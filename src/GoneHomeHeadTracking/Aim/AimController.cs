using System;
using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Computes aim offset from head tracking rotation by projecting the clean
    /// (mouse-controlled) aim direction through the head-tracked view matrix.
    /// This works in both yaw modes because the projection uses whatever
    /// worldToCameraMatrix the CameraController set this frame.
    /// </summary>
    public sealed class AimController
    {
        // Fixed projection distance - Gone Home is a walking sim with no weapons,
        // so we don't need to track per-frame hitpoints. A fixed distance gives
        // a stable reticle that doesn't hop between colliders.
        private const float ProjectionDistance = 10f;

        private readonly CameraController _cameraController;

        private Vector2 _screenOffset;

        public Vector2 ScreenOffset => _screenOffset;

        public AimController(CameraController cameraController)
        {
            if (cameraController == null)
            {
                throw new ArgumentNullException(nameof(cameraController), "CameraController cannot be null");
            }
            _cameraController = cameraController;
            _screenOffset = Vector2.zero;
        }

        public void UpdateAim(Camera camera)
        {
            // Camera may legitimately be null during Unity scene transitions - this is expected.
            if (camera == null) return;

            // camera.transform is unmodified (view matrix only), so transform.forward IS the aim direction.
            Vector3 aimDir = camera.transform.forward;
            _screenOffset = CanvasCompensation.CalculateAimScreenOffset(camera, aimDir, ProjectionDistance, 1f);
        }
    }
}
