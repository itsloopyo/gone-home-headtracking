using System;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Helper component attached to the main camera to apply head tracking at the right time.
    ///
    /// LOOK/AIM DECOUPLING via view matrix:
    /// Head tracking modifies only camera.worldToCameraMatrix - the camera transform is never touched.
    /// - Game logic (FrobManager, interactions) sees the un-tracked transform = AIM
    /// - Rendering sees the modified view matrix = LOOK (where head is pointing)
    /// </summary>
    public sealed class CameraTrackingHook : MonoBehaviour
    {
        private CameraController _cameraController;
        private readonly AimController _aimController = new AimController();
        private readonly GameReticle _gameReticle = new GameReticle();
        private readonly InteractionTextPositioner _interactionText = new InteractionTextPositioner();
        private Camera _camera;
        private bool _isEnabled;
        private bool _preCullErrorLogged;

        // worldToCameraMatrix is a sticky override: once written, Unity stops deriving it from
        // the transform until ResetWorldToCameraMatrix. Every path that stops tracking has to
        // reset it, or the view freezes where it was while the player walks on.
        private bool _matrixOverridden;

        // The frame the view was last tracked, and whether the game wanted its crosshair then.
        // OnGUI runs after this frame's OnPreCull, so a stamp from an earlier frame means this
        // camera did not render tracked this frame.
        private int _trackedFrame = -1;
        private bool _gameWantsReticle;

        // Gameplay detection - only apply tracking when vp_FPSCamera is active
        private Behaviour _cachedFPSCamera;
        private Component _frobManager;
        private bool _fpsCameraSearched;

        public void Initialize(CameraController cameraController, bool enabled)
        {
            _cameraController = cameraController;
            _camera = GetComponent<Camera>();
            _isEnabled = enabled;
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        /// <summary>
        /// The mod's reticle position as a pixel offset from screen center, while this frame is
        /// tracked and the game wants a crosshair shown.
        /// </summary>
        public bool TryGetReticleOffset(out Vector2 offset)
        {
            offset = _aimController.ScreenOffset;
            return _trackedFrame == Time.frameCount && _gameWantsReticle;
        }

        /// <summary>
        /// Checks if we're in gameplay by looking for an ENABLED vp_FPSCamera on/near the camera.
        /// Only applies tracking during actual gameplay, not menus/splash.
        /// </summary>
        private bool CheckInGameplay()
        {
            // MUST use Unity's == for destroyed object detection (not ReferenceEquals)
            if (_cachedFPSCamera != null)
            {
                return _cachedFPSCamera.enabled && _cachedFPSCamera.gameObject.activeInHierarchy;
            }

            if (_fpsCameraSearched) return false;
            _fpsCameraSearched = true;

            Type fpsCameraType = GameTypeResolver.FPSCameraType;
            if (NullHelper.IsNull(fpsCameraType)) return false;

            Component fpsCamera = _camera.GetComponent(fpsCameraType);
            Transform parent = _camera.transform.parent;
            if (fpsCamera == null && parent != null)
            {
                fpsCamera = parent.GetComponent(fpsCameraType);
            }
            if (fpsCamera == null) return false;

            _cachedFPSCamera = (Behaviour)fpsCamera;
            _frobManager = FindFrobManager(fpsCamera.transform);
            return _cachedFPSCamera.enabled && _cachedFPSCamera.gameObject.activeInHierarchy;
        }

        // FrobManager.Awake finds its vp_FPSCamera with GetComponentInChildren, so it sits on
        // that object or one of its ancestors.
        private static Component FindFrobManager(Transform fpsCamera)
        {
            if (!GameTypeResolver.HasFrobRay) return null;

            Type frobManagerType = GameTypeResolver.FrobManagerType;
            for (Transform t = fpsCamera; t != null; t = t.parent)
            {
                Component frobManager = t.GetComponent(frobManagerType);
                if (frobManager != null) return frobManager;
            }
            ModLoader.Log("[CameraTrackingHook] No FrobManager above vp_FPSCamera; the reticle projects the aim direction");
            return null;
        }

        /// <summary>
        /// Called just before this camera renders, after every LateUpdate, so the game's camera
        /// code has already placed the transform this frame.
        /// </summary>
        private void OnPreCull()
        {
            try
            {
                if (!_isEnabled || !CheckInGameplay() || !_cameraController.ApplyTracking(_camera))
                {
                    StopTracking();
                    return;
                }
                _matrixOverridden = true;

                _aimController.UpdateAim(_camera, _frobManager);

                Component hud = GameTypeResolver.CurrentHud();
                if (hud != null)
                {
                    _gameWantsReticle = _gameReticle.Hide(hud);
                    _interactionText.Follow(hud, _aimController.ScreenOffset);
                }
                else
                {
                    _gameWantsReticle = false;
                }
                _trackedFrame = Time.frameCount;
            }
            catch (Exception ex)
            {
                // OnPreCull runs every frame, so a recurring fault would otherwise
                // write ~60 lines/sec into the log the user is asked to send in.
                if (!_preCullErrorLogged)
                {
                    _preCullErrorLogged = true;
                    ModLoader.Log($"[CameraTrackingHook] OnPreCull error (logged once): {ex}");
                }
            }
        }

        private void StopTracking()
        {
            if (_matrixOverridden)
            {
                _matrixOverridden = false;
                if (_camera != null)
                {
                    _camera.ResetWorldToCameraMatrix();
                }
            }
            _gameReticle.Restore();
            _interactionText.Restore();
        }

        private void OnDestroy()
        {
            // During application quit Unity tears objects down in an arbitrary order, so the HUD
            // objects may already be half-destroyed and SetActive on them throws. Nothing needs
            // undoing then.
            if (HeadTrackingMod.IsQuitting) return;
            StopTracking();
        }
    }
}
