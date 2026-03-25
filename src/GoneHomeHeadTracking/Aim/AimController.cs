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
        private const float MaxRaycastDistance = 1000f;
        private const float MinRaycastDistance = 0.5f;
        private const float DistanceSmoothingRate = 15f;
        private float _lastHitDistance = 100f;

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

            // camera.transform is unmodified (view matrix only), so transform.forward IS the aim direction.
            Vector3 aimDir = camera.transform.forward;

            RaycastHit hit;
            if (Physics.Raycast(camera.transform.position, aimDir, out hit, MaxRaycastDistance)
                && hit.distance >= MinRaycastDistance)
            {
                float t = 1f - Mathf.Exp(-DistanceSmoothingRate * Time.deltaTime);
                _lastHitDistance = Mathf.Lerp(_lastHitDistance, hit.distance, t);
            }

            _screenOffset = CanvasCompensation.CalculateAimScreenOffset(camera, aimDir, _lastHitDistance, 1f);
        }
    }
}
