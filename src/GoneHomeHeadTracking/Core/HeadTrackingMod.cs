using System;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Rendering;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Main head tracking MonoBehaviour - standalone version without BepInEx.
    /// Orchestrates UDP receiver, camera controller, and aim system components.
    /// </summary>
    public sealed class HeadTrackingMod : MonoBehaviour
    {
        public const string ModName = "Head Tracking";
        public const string ModVersion = "1.1.1";

        /// <summary>Singleton instance</summary>
        public static HeadTrackingMod Instance { get; private set; }

        private OpenTrackReceiver _receiver;
        private CameraController _cameraController;
        private AimController _aimController;
        private IMGUIReticle _reticleRenderer;
        private GameReticleFinder _gameReticleFinder;
        private InteractionTextPositioner _interactionTextPositioner;
        private bool _isEnabled;

        // Configuration
        private HeadTrackingConfig _config;

        // State
        private bool _wasConnected;
        private bool _aimSystemInitialized;
        private CameraTrackingHook _cameraHook;
        private Camera _cachedMainCamera;
        private int _cameraCheckCounter;
        private const int CameraCheckInterval = 30; // ~0.5s at 60fps


        private void Awake()
        {
            Instance = this;
            Log($"Initializing {ModName} v{ModVersion}...");

            // Load config
            _config = HeadTrackingConfig.LoadFromFile(HeadTrackingConfig.GetDefaultConfigPath(), Log);

            // Initialize components
            _receiver = new OpenTrackReceiver();
            _receiver.Log = Log;
            _receiver.Start(_config.UdpPort);

            var processor = new TrackingProcessor
            {
                SmoothingFactor = _config.Smoothing,
                Sensitivity = new SensitivitySettings(
                    _config.YawSensitivity,
                    _config.PitchSensitivity,
                    _config.RollSensitivity,
                    invertYaw: false, invertPitch: false, invertRoll: false
                ),
                Deadzone = DeadzoneSettings.None
            };
            var interpolator = new PoseInterpolator();
            var positionProcessor = new PositionProcessor
            {
                TrackerPivotForward = 0.01f,
                Settings = new PositionSettings(
                    _config.PositionSensitivityX, _config.PositionSensitivityY, _config.PositionSensitivityZ,
                    float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue,
                    0f,
                    invertX: _config.InvertPositionX, invertY: _config.InvertPositionY, invertZ: _config.InvertPositionZ
                )
            };
            var positionInterpolator = new PositionInterpolator();
            _cameraController = new CameraController(_receiver, processor, interpolator, positionProcessor, positionInterpolator);

            // Aim system will be initialized lazily in Update() to avoid early init issues
            _aimSystemInitialized = false;

            _isEnabled = true;

            Log($"{ModName} loaded! Port: {_config.UdpPort}, Toggle: {_config.ToggleKey}, Recenter: {_config.RecenterKey}");
        }

        private void Update()
        {
            // Lazy init aim system after game is loaded
            if (!_aimSystemInitialized && _cameraController != null)
            {
                InitializeAimSystem();
            }

            // Hotkey checks: Input.anyKeyDown short-circuits the lookups on the
            // overwhelming majority of frames where no key transition occurs.
            // Two equivalent binding sets per the project standard: the configurable
            // nav-cluster key, OR the fixed Ctrl+Shift+<T/Y/G> chord.
            if (Input.anyKeyDown)
            {
                if (Input.GetKeyDown(_config.RecenterKey) || ChordPressed(KeyCode.T))
                {
                    Recenter();
                }

                if (Input.GetKeyDown(_config.ToggleKey) || ChordPressed(KeyCode.Y))
                {
                    ToggleTracking();
                }

                if (Input.GetKeyDown(_config.PositionToggleKey) || ChordPressed(KeyCode.G))
                {
                    _cameraController.PositionEnabled = !_cameraController.PositionEnabled;
                    Log($"Position tracking {(_cameraController.PositionEnabled ? "enabled" : "disabled")}");
                }
            }


            // Monitor connection state
            bool isConnected = _receiver != null && _receiver.IsReceiving;
            if (isConnected != _wasConnected)
            {
                _wasConnected = isConnected;
                Log(isConnected ? "OpenTrack connected" : "OpenTrack disconnected");
            }
        }

        private static bool ChordPressed(KeyCode letter)
        {
            if (!Input.GetKeyDown(letter)) return false;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return ctrl && shift;
        }

        private void LateUpdate()
        {
            // Ensure camera hook is attached to the main camera
            // The hook uses OnPreCull() which runs after all LateUpdate() calls,
            // ensuring the game's camera code can't overwrite our tracking rotation

            // Fast path: cached camera still valid, skip expensive Camera.main lookup
            // Unity's == returns true for destroyed objects, so this catches destruction immediately
            if (_cameraHook != null && _cachedMainCamera != null)
            {
                _cameraCheckCounter++;
                if (_cameraCheckCounter < CameraCheckInterval)
                    return;
                _cameraCheckCounter = 0;
            }

            // Slow path: validate or find camera via Camera.main (FindObjectWithTag)
            Camera currentMain = Camera.main;
            if (currentMain == null) return;

            // Check if we need to attach hook to a new camera
            if (_cameraHook == null || _cachedMainCamera != currentMain)
            {
                // Remove old hook if exists
                if (_cameraHook != null)
                {
                    Destroy(_cameraHook);
                    _cameraHook = null;
                }

                _cachedMainCamera = currentMain;
                _cameraCheckCounter = 0;

                // Add hook to camera GameObject
                _cameraHook = _cachedMainCamera.gameObject.AddComponent<CameraTrackingHook>();
                _cameraHook.Initialize(_cameraController, _aimController, _gameReticleFinder, _interactionTextPositioner, _receiver);
                _cameraHook.SetEnabled(_isEnabled);
            }
        }

        private void InitializeAimSystem()
        {
            if (_cameraController == null) return; // Not ready yet

            _aimController = new AimController(_cameraController);

            _gameReticleFinder = new GameReticleFinder();

            // Create interaction text positioner to move "Open Door" etc. to follow crosshair
            _interactionTextPositioner = new InteractionTextPositioner();

            // Create reticle renderer as MonoBehaviour on same GameObject
            // Check for existing renderer to prevent duplicates on retry
            _reticleRenderer = gameObject.GetComponent<IMGUIReticle>();
            if (NullHelper.IsNull(_reticleRenderer))
            {
                _reticleRenderer = gameObject.AddComponent<IMGUIReticle>();
            }
            _reticleRenderer.Initialize(GetReticlePosition);
            _reticleRenderer.ReticleColor = _config.ReticleColor;
            _reticleRenderer.IsVisible = _config.ShowReticle;

            // Hide the game's crosshair - we'll draw our own at the correct aim position
            _gameReticleFinder.TryHideGameReticle();

            // Update hook with aim components
            if (_cameraHook != null)
            {
                _cameraHook.SetAimComponents(_aimController, _gameReticleFinder, _interactionTextPositioner);
            }

            _aimSystemInitialized = true;
        }

        public void Recenter()
        {
            // These components are initialized in Awake() - if null, initialization failed
            if (_cameraController == null)
            {
                throw new InvalidOperationException("Cannot recenter: CameraController not initialized. Mod initialization failed.");
            }

            _cameraController.Recenter();
            Log("Recentered");
        }

        /// <summary>
        /// ReticlePositionProvider delegate for IMGUIReticle.
        /// Returns screen position for the reticle based on aim offset.
        /// </summary>
        private bool GetReticlePosition(out float screenX, out float screenY)
        {
            if (!CameraTrackingHook.IsInGameplay || !_config.ShowReticle || _aimController == null)
            {
                screenX = 0;
                screenY = 0;
                return false;
            }

            Vector2 offset = _aimController.ScreenOffset;
            screenX = Screen.width * 0.5f + offset.x;
            screenY = Screen.height * 0.5f + offset.y;
            return true;
        }

        public void ToggleTracking()
        {
            _isEnabled = !_isEnabled;
            Log(_isEnabled ? "Tracking enabled" : "Tracking disabled");

            // Update camera hook state
            if (_cameraHook != null)
            {
                _cameraHook.SetEnabled(_isEnabled);
            }

            if (_isEnabled)
            {
                // Re-hide game reticle and show custom reticle
                _gameReticleFinder?.TryHideGameReticle();
                if (_reticleRenderer != null)
                {
                    _reticleRenderer.IsVisible = _config.ShowReticle;
                }
            }
            else
            {
                _cameraController?.ResetCamera();

                // Restore game reticle when tracking disabled
                _gameReticleFinder?.RestoreGameReticle();
                if (_reticleRenderer != null)
                {
                    _reticleRenderer.IsVisible = false;
                }

                // Reset interaction text to original position
                _interactionTextPositioner?.ResetPosition();
            }
        }

        private void OnDestroy()
        {
            // Destroy camera hook if exists
            if (_cameraHook != null)
            {
                Destroy(_cameraHook);
                _cameraHook = null;
            }

            // Restore game reticle before cleanup
            _gameReticleFinder?.RestoreGameReticle();

            // Reset interaction text position
            _interactionTextPositioner?.ResetPosition();

            _receiver?.Dispose();
            _cameraController?.ResetCamera();
            Instance = null;

            // Schedule recreation on next frame
            ModLoader.ScheduleRecreate();
        }

        private static void Log(string message)
        {
            ModLoader.Log($"[Mod] {message}");
        }
    }
}
