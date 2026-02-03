using System;
using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Computes aim offset from head tracking rotation.
    /// Uses shared CanvasCompensation utilities from cameraunlock-core.
    /// </summary>
    public sealed class AimController
    {
        private readonly CameraController _cameraController;

        // Computed state
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

        /// <summary>
        /// Computes aim offset using shared cameraunlock-core utilities.
        /// Uses WorldToScreenPoint projection which correctly handles all rotation combinations.
        /// </summary>
        /// <param name="camera">The camera to compute aim for. May be null during scene transitions (expected Unity state).</param>
        public void UpdateAim(Camera camera)
        {
            // Camera may legitimately be null during Unity scene transitions - this is expected
            if (camera == null) return;

            // Use shared cameraunlock-core utility for WorldToScreenPoint projection
            Quaternion trackingQuat = _cameraController.TrackingQuaternion;
            _screenOffset = CanvasCompensation.CalculateAimScreenOffsetFromTracking(camera, trackingQuat);
        }
    }
}
