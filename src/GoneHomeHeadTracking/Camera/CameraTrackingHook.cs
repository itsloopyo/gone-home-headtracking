using System;
using CameraUnlock.Core.Protocol;
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
        private AimController _aimController;
        private GameReticleFinder _gameReticleFinder;
        private InteractionTextPositioner _interactionTextPositioner;
        private OpenTrackReceiver _receiver;
        private Camera _camera;
        private bool _isEnabled;
        private bool _preCullErrorLogged;

        // Gameplay detection - only apply tracking when vp_FPSCamera is active
        private static bool _staticIsInGameplay;
        private Behaviour _cachedFPSCamera;
        private bool _fpsCameraSearched;
        private bool _isInGameplay;

        /// <summary>
        /// Returns true if the player currently has control (vp_FPSCamera is enabled).
        /// Used by ReticleRenderer to hide reticle during cutscenes/menus.
        /// </summary>
        public static bool IsInGameplay => _staticIsInGameplay;

        /// <summary>
        /// Initializes the hook with references to the tracking components.
        /// </summary>
        public void Initialize(
            CameraController cameraController,
            AimController aimController,
            GameReticleFinder gameReticleFinder,
            InteractionTextPositioner interactionTextPositioner,
            OpenTrackReceiver receiver)
        {
            _cameraController = cameraController;
            _aimController = aimController;
            _gameReticleFinder = gameReticleFinder;
            _interactionTextPositioner = interactionTextPositioner;
            _receiver = receiver;
            _camera = GetComponent<Camera>();
            _isEnabled = true;
        }

        /// <summary>
        /// Updates references when aim system is initialized later.
        /// </summary>
        public void SetAimComponents(AimController aimController, GameReticleFinder gameReticleFinder, InteractionTextPositioner interactionTextPositioner)
        {
            _aimController = aimController;
            _gameReticleFinder = gameReticleFinder;
            _interactionTextPositioner = interactionTextPositioner;
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        /// <summary>
        /// Checks if we're in gameplay by looking for an ENABLED vp_FPSCamera on/near the camera.
        /// Only applies tracking during actual gameplay, not menus/splash.
        /// Caches the component reference to avoid expensive GetComponent calls every frame.
        /// </summary>
        private bool CheckInGameplay()
        {
            // Use cached component if available and still valid
            // MUST use Unity's == for destroyed object detection (not ReferenceEquals)
            if (_cachedFPSCamera != null)
            {
                // Quick check - just verify it's still enabled
                return _cachedFPSCamera.enabled && _cachedFPSCamera.gameObject.activeInHierarchy;
            }

            // Only search once per hook instance
            if (_fpsCameraSearched) return false;

            // Search for vp_FPSCamera on this camera or its parent
            // NullHelper OK for Type (not Unity object), but use != null for Unity objects
            Type fpseCameraType = GameTypeResolver.FPSCameraType;
            if (NullHelper.NotNull(fpseCameraType) && _camera != null)
            {
                // Check on camera itself
                Component comp = _camera.GetComponent(fpseCameraType);
                if (comp != null)
                {
                    _cachedFPSCamera = comp as Behaviour;
                    _fpsCameraSearched = true;
                    return _cachedFPSCamera != null &&
                           _cachedFPSCamera.enabled &&
                           _cachedFPSCamera.gameObject.activeInHierarchy;
                }

                // Check on parent
                Transform parent = _camera.transform.parent;
                if (parent != null)
                {
                    comp = parent.GetComponent(fpseCameraType);
                    if (comp != null)
                    {
                        _cachedFPSCamera = comp as Behaviour;
                        _fpsCameraSearched = true;
                        return _cachedFPSCamera != null &&
                               _cachedFPSCamera.enabled &&
                               _cachedFPSCamera.gameObject.activeInHierarchy;
                    }
                }
            }

            _fpsCameraSearched = true;
            return false;
        }

        /// <summary>
        /// Called just before this camera renders.
        ///
        /// LOOK/AIM DECOUPLING via view matrix:
        /// Head tracking modifies only worldToCameraMatrix - the camera transform stays unchanged.
        /// Game logic (FrobManager, raycasts) sees the un-tracked transform = AIM direction.
        /// Rendering sees the modified view matrix = LOOK direction (where head is pointing).
        /// No OnPostRender restoration needed.
        /// </summary>
        private void OnPreCull()
        {
            try
            {
                // Check if we're in gameplay
                _isInGameplay = CheckInGameplay();
                _staticIsInGameplay = _isInGameplay;

                bool gameStateAllowsTracking = _isInGameplay && _isEnabled;
                bool canTrack = gameStateAllowsTracking &&
                                NullHelper.NotNull(_cameraController) && NullHelper.NotNull(_receiver) &&
                                _receiver.IsReceiving && _camera != null;

                if (!canTrack)
                {
                    return;
                }

                // Apply head tracking via view matrix - transform stays untouched
                _cameraController.ApplyTracking(_camera);

                // Update aim controller with tracking info
                // AimController computes screen offset for where "aim" appears relative to "look"
                if (NullHelper.NotNull(_aimController))
                {
                    _aimController.UpdateAim(_camera);

                    // Update interaction text position to follow the crosshair
                    _interactionTextPositioner?.UpdatePosition(_aimController.ScreenOffset);
                }

                // Keep trying to hide game reticle until we find it
                _gameReticleFinder?.TryHideGameReticle();

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

    }
}
