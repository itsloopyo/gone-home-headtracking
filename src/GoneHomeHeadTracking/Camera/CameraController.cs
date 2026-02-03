using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Applies head tracking rotation to the game camera additively.
    /// Rotation is applied on top of existing mouse/controller look to preserve normal controls.
    /// Delegates to shared TrackingProcessor (sensitivity, recenter, smoothing, deadzone)
    /// and PoseInterpolator (inter-sample interpolation).
    /// </summary>
    public sealed class CameraController
    {
        private readonly OpenTrackReceiver _receiver;
        private readonly TrackingProcessor _processor;
        private readonly PoseInterpolator _interpolator;
        private readonly float _smoothingFactor;
        private readonly PositionProcessor _positionProcessor;
        private readonly PositionInterpolator _positionInterpolator;

        private Camera _targetCamera;
        private Vec3 _lastPositionOffset;
        private bool _hasCentered;

        // Tracking-only quaternion for aim compensation
        private Quaternion _trackingQuaternion = Quaternion.identity;

        // Output SLERP smoothing state (second smoothing layer for remote connections)
        private Quaternion _smoothedTrackingQuat = Quaternion.identity;
        private bool _hasSmoothedTracking;

        /// <summary>
        /// Gets the tracking-only quaternion (smoothed for remote connections).
        /// Used by AimController to compute the aim offset.
        /// </summary>
        public Quaternion TrackingQuaternion => _trackingQuaternion;

        /// <summary>Whether positional tracking is enabled.</summary>
        public bool PositionEnabled { get; set; } = true;

        /// <summary>Last applied position offset for transition fadeout.</summary>
        public Vec3 LastPositionOffset => _lastPositionOffset;

        /// <summary>
        /// Creates a new camera controller for applying head tracking.
        /// </summary>
        public CameraController(OpenTrackReceiver receiver, TrackingProcessor processor, PoseInterpolator interpolator, float smoothingFactor,
            PositionProcessor positionProcessor, PositionInterpolator positionInterpolator)
        {
            _receiver = receiver;
            _processor = processor;
            _interpolator = interpolator;
            _smoothingFactor = smoothingFactor;
            _positionProcessor = positionProcessor;
            _positionInterpolator = positionInterpolator;
        }

        /// <summary>
        /// Sets the current head position as the center reference.
        /// </summary>
        public void Recenter()
        {
            var rawPose = _receiver.GetLatestPose();
            _processor.RecenterTo(rawPose);
            _interpolator.Reset();
            _smoothedTrackingQuat = Quaternion.identity;
            _hasSmoothedTracking = false;
            _positionProcessor?.SetCenter(_receiver.GetLatestPosition());
            _positionInterpolator?.Reset();
            _lastPositionOffset = Vec3.Zero;
        }

        /// <summary>
        /// Applies head tracking rotation to the specified camera.
        /// Called by CameraTrackingHook.OnPreCull() with the hook's camera.
        /// </summary>
        public void ApplyTracking(Camera camera)
        {
            if (camera == null) return;
            _targetCamera = camera;

            // Auto-recenter on first valid frame so the user's startup head position is neutral
            if (!_hasCentered)
            {
                _hasCentered = true;
                Recenter();
            }

            // Get raw tracking data, interpolate between samples, then process
            var rawPose = _receiver.GetLatestPose();
            var interpolated = _interpolator.Update(rawPose, Time.deltaTime);
            bool isRemote = _receiver.IsRemoteConnection;
            var processed = _processor.Process(interpolated, isRemote, Time.deltaTime);

            float headYaw = processed.Yaw;
            float headPitch = processed.Pitch;
            float headRoll = processed.Roll;

            // Build tracking quaternion (Y-X-Z axis ordering)
            Quaternion trackingQuat = Quaternion.AngleAxis(headYaw, Vector3.up)
                * Quaternion.AngleAxis(-headPitch, Vector3.right)
                * Quaternion.AngleAxis(headRoll, Vector3.forward);

            // Output SLERP smoothing: second smoothing layer that eliminates
            // snap-to-raw artifacts from PoseInterpolator on remote connections.
            // For local connections (smoothing=0), t=1 so Slerp returns target unchanged.
            if (_hasSmoothedTracking)
            {
                float outputSmoothing = isRemote
                    ? Mathf.Clamp01(_smoothingFactor + SmoothingUtils.RemoteConnectionBaseline)
                    : _smoothingFactor;
                float t = SmoothingUtils.CalculateSmoothingFactor(outputSmoothing, Time.deltaTime);
                trackingQuat = Quaternion.Slerp(_smoothedTrackingQuat, trackingQuat, t);
            }
            _smoothedTrackingQuat = trackingQuat;
            _hasSmoothedTracking = true;

            _trackingQuaternion = trackingQuat;

            // Compose: game rotation * tracking (tracking in game-local space)
            Quaternion gameRotation = _targetCamera.transform.rotation;
            _targetCamera.transform.rotation = gameRotation * trackingQuat;

            // Position tracking: use tracker 6DOF data via PositionProcessor
            if (PositionEnabled && _positionProcessor != null)
            {
                var rawPos = _receiver.GetLatestPosition();
                var interpolatedPos = _positionInterpolator.Update(rawPos, Time.deltaTime);

                var headRotQ = new Quat4(trackingQuat.x, trackingQuat.y, trackingQuat.z, trackingQuat.w);
                _lastPositionOffset = _positionProcessor.Process(interpolatedPos, headRotQ, isRemote, Time.deltaTime);

                // Apply in camera space: forward means camera forward so leaning
                // in moves toward whatever you're looking at.
                Vector3 trackingOffset = new Vector3(_lastPositionOffset.X, _lastPositionOffset.Y, _lastPositionOffset.Z);
                _targetCamera.transform.position += gameRotation * trackingOffset;
            }
        }

        public void ResetCamera()
        {
            _trackingQuaternion = Quaternion.identity;
            _smoothedTrackingQuat = Quaternion.identity;
            _hasSmoothedTracking = false;
            _processor.ResetSmoothing();
            _interpolator.Reset();
            _positionProcessor?.Reset();
            _positionInterpolator?.Reset();
            _lastPositionOffset = Vec3.Zero;
        }
    }
}
