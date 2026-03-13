using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Tracking;
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
        private readonly PositionProcessor _positionProcessor;
        private readonly PositionInterpolator _positionInterpolator;

        private Camera _targetCamera;
        private Vec3 _lastPositionOffset;
        private bool _hasCentered;

        // Tracking-only quaternion for aim compensation
        private Quaternion _trackingQuaternion = Quaternion.identity;

        /// <summary>
        /// Gets the tracking-only quaternion (smoothed).
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
        public CameraController(OpenTrackReceiver receiver, TrackingProcessor processor, PoseInterpolator interpolator,
            PositionProcessor positionProcessor, PositionInterpolator positionInterpolator)
        {
            _receiver = receiver;
            _processor = processor;
            _interpolator = interpolator;
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

            // Auto-recenter once real tracker data arrives so the user's startup position is neutral.
            // Wait for fresh data — on the very first frame the receiver may not have packets yet.
            if (!_hasCentered)
            {
                if (!_receiver.IsDataFresh()) return;
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

            // Apply rotation via view matrix — never touch camera.transform.
            // Pitch negated to match ComposeAdditive convention (positive pitch = look up).
            var neckPivot = new Vector3(0f, 0.10f, 0.08f);
            ViewMatrixModifier.ApplyHeadRotationDecomposed(camera, headYaw, -headPitch, headRoll, neckPivot);

            _trackingQuaternion = CameraRotationComposer.GetTrackingOnlyRotation(headYaw, headPitch, headRoll);

            // Position tracking: use tracker 6DOF data via PositionProcessor
            if (PositionEnabled && _positionProcessor != null)
            {
                var rawPos = _receiver.GetLatestPosition();
                var interpolatedPos = _positionInterpolator.Update(rawPos, Time.deltaTime);

                var headRotQ = new Quat4(_trackingQuaternion.x, _trackingQuaternion.y, _trackingQuaternion.z, _trackingQuaternion.w);
                _lastPositionOffset = _positionProcessor.Process(interpolatedPos, headRotQ, isRemote, Time.deltaTime);

                // Apply position offset via view matrix translation.
                // Camera-local position: leaning forward moves toward whatever
                // you're looking at, so you can inspect objects on surfaces.
                Quaternion gameRotation = camera.transform.rotation;
                Vector3 worldOffset = PositionApplicator.ToCameraLocalWorld(_lastPositionOffset, gameRotation);
                Matrix4x4 vm = camera.worldToCameraMatrix;
                Vector3 viewSpaceOffset = vm.MultiplyVector(worldOffset);
                vm.m03 -= viewSpaceOffset.x;
                vm.m13 -= viewSpaceOffset.y;
                vm.m23 -= viewSpaceOffset.z;
                camera.worldToCameraMatrix = vm;
            }
        }

        /// <summary>
        /// Re-arms the auto-recenter so the next ApplyTracking call with fresh data
        /// will recenter automatically. Called when tracking is lost (disconnect,
        /// toggle off, leaving gameplay) so reconnection feels seamless.
        /// </summary>
        public void NotifyTrackingLost()
        {
            _hasCentered = false;
        }

        public void ResetCamera()
        {
            _trackingQuaternion = Quaternion.identity;
            _processor.ResetSmoothing();
            _interpolator.Reset();
            _positionProcessor?.Reset();
            _positionInterpolator?.Reset();
            _lastPositionOffset = Vec3.Zero;
        }
    }
}
