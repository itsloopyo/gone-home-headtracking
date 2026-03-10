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

        // Output smoothing state (second smoothing layer for remote connections)
        private float _smoothedYaw, _smoothedPitch, _smoothedRoll;
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
            _smoothedYaw = _smoothedPitch = _smoothedRoll = 0f;
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

            // Output smoothing: second smoothing layer that eliminates
            // snap-to-raw artifacts from PoseInterpolator on remote connections.
            // For local connections (smoothing=0), t=1 so Lerp returns target unchanged.
            if (_hasSmoothedTracking)
            {
                float outputSmoothing = isRemote
                    ? Mathf.Clamp01(_smoothingFactor + SmoothingUtils.RemoteConnectionBaseline)
                    : _smoothingFactor;
                float t = SmoothingUtils.CalculateSmoothingFactor(outputSmoothing, Time.deltaTime);
                headYaw = Mathf.Lerp(_smoothedYaw, headYaw, t);
                headPitch = Mathf.Lerp(_smoothedPitch, headPitch, t);
                headRoll = Mathf.Lerp(_smoothedRoll, headRoll, t);
            }
            _smoothedYaw = headYaw;
            _smoothedPitch = headPitch;
            _smoothedRoll = headRoll;
            _hasSmoothedTracking = true;

            Quaternion gameRotation = _targetCamera.transform.rotation;

            // Spherical-coordinate projection (matching DL2): yaw sweeps along
            // gameRight (always horizontal), pitch elevates along gameUp, so yaw
            // gives constant horizontal displacement regardless of game camera pitch.
            // This avoids the quaternion-sandwich problem where world-up yaw appears
            // as roll when the camera is pitched down.
            Vector3 gameFwd = gameRotation * Vector3.forward;
            Vector3 gameUp = gameRotation * Vector3.up;
            Vector3 gameRight = gameRotation * Vector3.right;

            float yawRad = headYaw * Mathf.Deg2Rad;
            float pitchRad = headPitch * Mathf.Deg2Rad;
            float cosY = Mathf.Cos(yawRad);
            float sinY = Mathf.Sin(yawRad);
            float cosP = Mathf.Cos(pitchRad);
            float sinP = Mathf.Sin(pitchRad);

            Vector3 newFwd = (cosP * cosY * gameFwd
                + cosP * sinY * gameRight
                + sinP * gameUp).normalized;

            // Re-derive up perpendicular to new forward (Gram-Schmidt against game up)
            Vector3 newUp = (gameUp - newFwd * Vector3.Dot(newFwd, gameUp)).normalized;

            // Apply roll via Rodrigues rotation around new forward
            float cosR = Mathf.Cos(headRoll * Mathf.Deg2Rad);
            float sinR = Mathf.Sin(headRoll * Mathf.Deg2Rad);
            newUp = (newUp * cosR + Vector3.Cross(newFwd, newUp) * sinR).normalized;

            Quaternion finalRotation = Quaternion.LookRotation(newFwd, newUp);
            _targetCamera.transform.rotation = finalRotation;

            _trackingQuaternion = Quaternion.Inverse(gameRotation) * finalRotation;

            // Position tracking: use tracker 6DOF data via PositionProcessor
            if (PositionEnabled && _positionProcessor != null)
            {
                var rawPos = _receiver.GetLatestPosition();
                var interpolatedPos = _positionInterpolator.Update(rawPos, Time.deltaTime);

                var headRotQ = new Quat4(_trackingQuaternion.x, _trackingQuaternion.y, _trackingQuaternion.z, _trackingQuaternion.w);
                _lastPositionOffset = _positionProcessor.Process(interpolatedPos, headRotQ, isRemote, Time.deltaTime);

                // Camera-local position: leaning forward moves toward whatever
                // you're looking at, so you can inspect objects on surfaces.
                Vector3 trackingOffset = new Vector3(_lastPositionOffset.X, _lastPositionOffset.Y, _lastPositionOffset.Z);
                _targetCamera.transform.position += gameRotation * trackingOffset;
            }
        }

        public void ResetCamera()
        {
            _trackingQuaternion = Quaternion.identity;
            _smoothedYaw = _smoothedPitch = _smoothedRoll = 0f;
            _hasSmoothedTracking = false;
            _processor.ResetSmoothing();
            _interpolator.Reset();
            _positionProcessor?.Reset();
            _positionInterpolator?.Reset();
            _lastPositionOffset = Vec3.Zero;
        }
    }
}
