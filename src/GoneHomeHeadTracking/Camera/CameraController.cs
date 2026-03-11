using CameraUnlock.Core.Data;
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
            float headPitch = -processed.Pitch;
            float headRoll = processed.Roll;

            Quaternion gameRotation = _targetCamera.transform.rotation;

            // Horizon-locked yaw (matching DL2 rotation_math.h): yaw rotates
            // around world Y, pitch around camera right. Pure yaw preserves
            // the vertical angle regardless of game camera pitch, and horizontal
            // displacement naturally scales with cos(gamePitch).
            Vector3 newFwd = gameRotation * Vector3.forward;
            Vector3 newUp = gameRotation * Vector3.up;

            // Yaw: rotate fwd and up around world Y axis (horizon-locked)
            float yawRad = headYaw * Mathf.Deg2Rad;
            if (Mathf.Abs(yawRad) >= 0.001f)
            {
                float cosY = Mathf.Cos(yawRad);
                float sinY = Mathf.Sin(yawRad);
                newFwd = RodriguesRotate(newFwd, Vector3.up, cosY, sinY);
                newUp = RodriguesRotate(newUp, Vector3.up, cosY, sinY);
            }

            // Pitch: rotate fwd around camera's right vector
            float pitchRad = headPitch * Mathf.Deg2Rad;
            if (Mathf.Abs(pitchRad) >= 0.001f)
            {
                Vector3 right = Vector3.Cross(newUp, newFwd).normalized;
                float cosP = Mathf.Cos(pitchRad);
                float sinP = Mathf.Sin(pitchRad);
                newFwd = RodriguesRotate(newFwd, right, cosP, sinP);
            }

            // Re-derive up perpendicular to new forward (Gram-Schmidt against yaw-rotated up)
            newUp = (newUp - newFwd * Vector3.Dot(newFwd, newUp)).normalized;

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

        /// <summary>
        /// Rodrigues rotation: rotate v around a unit axis by angle with precomputed cos/sin.
        /// v' = v*cos + (axis x v)*sin + axis*(axis . v)*(1 - cos)
        /// </summary>
        private static Vector3 RodriguesRotate(Vector3 v, Vector3 axis, float cos, float sin)
        {
            float dot = Vector3.Dot(axis, v);
            Vector3 cross = Vector3.Cross(axis, v);
            float omc = 1f - cos;
            return v * cos + cross * sin + axis * (dot * omc);
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
