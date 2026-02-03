using System;
using CameraUnlock.Core.Protocol;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Helper component attached to the main camera to apply head tracking at the right time.
    ///
    /// CRITICAL FOR LOOK/AIM DECOUPLING:
    /// We apply tracking ONLY during rendering (OnPreCull) and restore the original rotation
    /// after rendering (OnPostRender). This means:
    /// - Game logic (FrobManager, interactions) sees the un-tracked camera direction = AIM
    /// - Rendering sees the tracked camera direction = LOOK (where head is pointing)
    ///
    /// This decouples look from aim - you can look around with head tracking while
    /// interactions/aiming remain at screen center.
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

        // Rotation/position restoration for look/aim decoupling
        // We store the game's rotation and position before tracking, apply tracking for render,
        // then restore after render so game logic sees un-tracked direction
        private Quaternion _preTrackingRotation;
        private Vector3 _preTrackingPosition;
        private bool _trackingAppliedThisFrame;

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
        /// LOOK/AIM DECOUPLING:
        /// 1. Store the current (game-controlled) rotation - this is the AIM direction
        /// 2. Apply head tracking on top - this is the LOOK direction
        /// 3. Camera renders with LOOK direction
        /// 4. OnPostRender restores AIM direction for next frame's game logic
        /// </summary>
        private void OnPreCull()
        {
            _trackingAppliedThisFrame = false;

            try
            {
                // Check if we're in gameplay
                _isInGameplay = CheckInGameplay();
                _staticIsInGameplay = _isInGameplay;
                if (!_isInGameplay) return;

                if (!_isEnabled || NullHelper.IsNull(_cameraController) || NullHelper.IsNull(_receiver) || !_receiver.IsReceiving)
                    return;

                if (_camera == null) return;

                // CRITICAL: Store the pre-tracking rotation and position (this is the AIM direction)
                // The game's vp_FPSCamera has set this in LateUpdate - it represents
                // where the player is aiming based on mouse/controller input
                _preTrackingRotation = _camera.transform.rotation;
                _preTrackingPosition = _camera.transform.position;
                _trackingAppliedThisFrame = true;

                // Apply head tracking to camera - this is the LOOK direction
                // After this, camera.forward = where player's head is looking
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
                ModLoader.Log($"[CameraTrackingHook] OnPreCull error: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after this camera has finished rendering.
        ///
        /// CRITICAL FOR LOOK/AIM DECOUPLING:
        /// Restore the pre-tracking rotation so that game logic (FrobManager, etc.)
        /// sees the AIM direction (un-tracked) during the next frame's Update phase.
        ///
        /// Without this, FrobManager would raycast from the LOOK direction,
        /// meaning interactions would follow where you look instead of screen center.
        /// </summary>
        private void OnPostRender()
        {
            if (!_trackingAppliedThisFrame) return;

            try
            {
                if (_camera != null)
                {
                    // Restore the AIM direction and position for game logic
                    _camera.transform.rotation = _preTrackingRotation;
                    _camera.transform.position = _preTrackingPosition;
                }
            }
            catch (Exception ex)
            {
                ModLoader.Log($"[CameraTrackingHook] OnPostRender error: {ex.Message}");
            }
        }
    }
}
