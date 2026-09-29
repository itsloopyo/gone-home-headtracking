using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Places the reticle where the game's interaction ray lands, seen through the head-tracked
    /// view. camera.transform is never modified, so its position and forward are the clean eye
    /// and aim the game's FrobManager casts from.
    /// </summary>
    public sealed class AimController
    {
        public Vector2 ScreenOffset { get; private set; }

        /// <param name="frobManager">The player's FrobManager, or null when it was not found.</param>
        public void UpdateAim(Camera camera, Component frobManager)
        {
            Transform eye = camera.transform;
            Vector3 aim = eye.forward;

            // The same ray FrobManager.GetFrobbableUnderCameraCenter casts. A lean moves the
            // rendered eye off the clean one, so the reticle has to sit on the surface that ray
            // hits: any fixed depth is right at that depth only. With no hit, the far plane
            // stands in for the aim direction itself.
            float depth = camera.farClipPlane;
            RaycastHit hit;
            if (frobManager != null
                && Physics.Raycast(new Ray(eye.position, aim), out hit,
                    GameTypeResolver.MaxFrobDistance(frobManager), GameTypeResolver.FrobLayerMask(frobManager)))
            {
                depth = hit.distance;
            }

            ScreenOffset = CanvasCompensation.CalculateAimScreenOffset(camera, aim, depth, 1f);
        }
    }
}
