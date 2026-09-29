using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Applies head tracking rotation to the game camera additively.
    /// Rotation is applied on top of existing mouse/controller look to preserve normal controls.
    /// Delegates to shared TrackingProcessor (smoothing) and PoseInterpolator (inter-sample
    /// interpolation).
    /// </summary>
    public sealed class CameraController
    {
        private readonly OpenTrackReceiver _receiver;
        private readonly TrackingProcessor _processor;
        private readonly PoseInterpolator _interpolator;
        private readonly PositionProcessor _positionProcessor;
        private readonly PositionInterpolator _positionInterpolator;

        /// <summary>Whether positional tracking is enabled.</summary>
        public bool PositionEnabled { get; set; } = true;

        /// <summary>Whether rotational tracking is enabled.</summary>
        public bool RotationEnabled { get; set; } = true;

        /// <summary>
        /// Yaw rotation mode. true (default) = horizon-locked yaw around world up
        /// (causes camera arc at extreme head yaw + mouse pitch - geometrically
        /// inherent to the composition). false = camera-local yaw (all axes composed
        /// in camera space; leans at extreme head yaw + mouse pitch).
        /// </summary>
        public bool WorldSpaceYaw { get; set; } = true;

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
        /// Writes the head-tracked view matrix to the camera. Returns false, leaving the camera
        /// alone, until the tracker has sent a first pose. After that the last pose is held
        /// through any gap in the data.
        /// </summary>
        public bool ApplyTracking(Camera camera)
        {
            var rawPose = _receiver.GetLatestPose();
            if (!rawPose.IsValid) return false;

            // Sample-rate-to-frame-rate interpolation is gated on receiving data, never on
            // the smoothing value: LocalSmoothing defaults to 0.0, and a smoothing-based gate
            // would leave every local user with stepped motion on a high-refresh display.
            rawPose = _interpolator.Update(rawPose, Time.deltaTime);

            // A connection change (local tracker <-> remote device) swaps which smoothing
            // parameter applies, so refresh the flag every frame from the receiver.
            bool isRemoteConnection = _receiver.IsRemoteConnection;
            _processor.IsRemoteConnection = isRemoteConnection;
            _positionProcessor.IsRemoteConnection = isRemoteConnection;

            var processed = _processor.Process(rawPose, Time.deltaTime);

            float headYaw = processed.Yaw;
            float headPitch = processed.Pitch;
            float headRoll = processed.Roll;

            if (RotationEnabled)
            {
                // Apply rotation via view matrix - never touch camera.transform.
                // Pitch negated to match Euler convention (positive pitch = look up).
                if (WorldSpaceYaw)
                {
                    ViewMatrixModifier.ApplyHeadRotationDecomposed(camera, headYaw, -headPitch, headRoll);
                }
                else
                {
                    ViewMatrixModifier.ApplyHeadRotation(camera, headYaw, -headPitch, headRoll);
                }
            }
            else
            {
                // Reset to clean state so any previously applied head rotation is cleared,
                // and so the position branch below reads a fresh game view matrix.
                camera.ResetWorldToCameraMatrix();
            }

            if (PositionEnabled)
            {
                var rawPos = _receiver.GetLatestPosition();
                var interpolatedPos = _positionInterpolator.Update(rawPos, Time.deltaTime);

                // The pivot arc comes from the physical head rotation, which the head still
                // makes in position-only mode, so it is taken whether or not rotation is applied.
                Quat4 physicalRotation = QuaternionUtils.FromYawPitchRoll(headYaw, headPitch, headRoll);
                Vec3 offset = _positionProcessor.Process(interpolatedPos, physicalRotation, Time.deltaTime);

                // Apply position offset via view matrix translation.
                // Camera-local position: leaning forward moves toward whatever
                // you're looking at, so you can inspect objects on surfaces.
                Vector3 worldOffset = PositionApplicator.ToCameraLocalWorld(offset, camera.transform.rotation);
                Matrix4x4 vm = camera.worldToCameraMatrix;
                Vector3 viewSpaceOffset = vm.MultiplyVector(worldOffset);
                vm.m03 -= viewSpaceOffset.x;
                vm.m13 -= viewSpaceOffset.y;
                vm.m23 -= viewSpaceOffset.z;
                camera.worldToCameraMatrix = vm;
            }

            return true;
        }

        public void ResetCamera()
        {
            _processor.ResetSmoothing();
            _interpolator.Reset();
            _positionProcessor.Reset();
            _positionInterpolator.Reset();
        }
    }
}
