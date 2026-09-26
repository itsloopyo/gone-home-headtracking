using System;
using System.Collections.Generic;
using System.IO;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Extensions;
using CameraUnlock.Core.Unity.Rendering;
using HeadTracking.Legacy;
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
        public const string ModVersion = "1.5.0";

        /// <summary>Singleton instance</summary>
        public static HeadTrackingMod Instance { get; private set; }

        private OpenTrackReceiver _receiver;
        private CameraController _cameraController;
        private AimController _aimController;
        private IMGUIReticle _reticleRenderer;
        private GameReticleFinder _gameReticleFinder;
        private InteractionTextPositioner _interactionTextPositioner;
        private bool _isEnabled;
        private TrackingMode _trackingMode;

        // Configuration
        private GoneHomeConfig _config;
        private ConfigOwner<GoneHomeConfig> _configOwner;
        private KeyBinding[] _toggleKeys;
        private KeyBinding[] _cycleTrackingModeKeys;
        private KeyBinding[] _yawModeKeys;

        // State
        private bool _wasConnected;
        private bool _aimSystemInitialized;
        private static bool _isQuitting;
        private CameraTrackingHook _cameraHook;
        private Camera _cachedMainCamera;
        private int _cameraCheckCounter;
        private const int CameraCheckInterval = 30; // ~0.5s at 60fps


        private void Awake()
        {
            Instance = this;
            Log($"Initializing {ModName} v{ModVersion}...");

            LoadConfig();

            // Initialize components
            _receiver = new OpenTrackReceiver();
            _receiver.Log = Log;
            _receiver.Start(_config.UdpPort);

            var processor = new TrackingProcessor
            {
                LocalSmoothing = _config.LocalSmoothing,
                RemoteSmoothing = _config.RemoteSmoothing,
                Sensitivity = SensitivitySettings.Default,
                Deadzone = DeadzoneSettings.None
            };
            var interpolator = new PoseInterpolator();
            var positionProcessor = new PositionProcessor
            {
                TrackerPivotForward = 0.01f,
                // Every published build shipped InvertPositionX=true and the other two false, and
                // applied them here as the mod's axis conversion. The settings are gone and their
                // shipped values stay in the code.
                Settings = PositionSettings.Symmetric(
                    1.0f, 1.0f, 1.0f,
                    float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue,
                    _config.LocalSmoothing, _config.RemoteSmoothing,
                    invertX: LegacyConfigImport.ShippedInvertPositionX,
                    invertY: LegacyConfigImport.ShippedInvertPositionY,
                    invertZ: LegacyConfigImport.ShippedInvertTrackerZ
                )
            };
            var positionInterpolator = new PositionInterpolator();
            _cameraController = new CameraController(_receiver, processor, interpolator, positionProcessor, positionInterpolator);
            _cameraController.WorldSpaceYaw = _config.WorldSpaceYaw;

            // The pair always names a mode: the table reads a pair that names none as its default.
            _trackingMode = TrackingModeChannels.Decode(_config.RotationEnabled, _config.PositionEnabled).Value;
            ApplyTrackingMode();

            // Aim system will be initialized lazily in Update() to avoid early init issues
            _aimSystemInitialized = false;

            _isEnabled = _config.EnableOnStartup;

            Log($"{ModName} loaded! Port: {_config.UdpPort}, Toggle: {_config.ToggleKeyName}, "
                + $"tracking {(_isEnabled ? "on" : "off")}, mode: {_trackingMode.Description()}");
        }

        private void Update()
        {
            // Lazy init aim system after game is loaded
            if (!_aimSystemInitialized && _cameraController != null)
            {
                InitializeAimSystem();
            }

            // Input.anyKeyDown short-circuits the lookups on the overwhelming majority of
            // frames where no key transition occurs.
            if (Input.anyKeyDown)
            {
                if (KeyBindingInput.IsTriggered(_toggleKeys))
                {
                    ToggleTracking();
                }

                if (KeyBindingInput.IsTriggered(_cycleTrackingModeKeys))
                {
                    CycleTrackingMode();
                }

                if (KeyBindingInput.IsTriggered(_yawModeKeys))
                {
                    ToggleYawMode();
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

        private void ToggleYawMode()
        {
            bool worldSpace = !_cameraController.WorldSpaceYaw;
            _cameraController.WorldSpaceYaw = worldSpace;
            Log(worldSpace
                ? "Yaw mode: world-space (horizon-locked)"
                : "Yaw mode: camera-local");
            SaveConfig(c => c.WorldSpaceYaw = worldSpace);
        }

        private void CycleTrackingMode()
        {
            _trackingMode = (TrackingMode)(((int)_trackingMode + 1) % 3);
            ApplyTrackingMode();
            Log($"Tracking mode: {_trackingMode.Description()}");

            bool rotation;
            bool position;
            TrackingModeChannels.Encode(_trackingMode, out rotation, out position);
            SaveConfig(c =>
            {
                c.RotationEnabled = rotation;
                c.PositionEnabled = position;
            });
        }

        private void ApplyTrackingMode()
        {
            bool rotation;
            bool position;
            TrackingModeChannels.Encode(_trackingMode, out rotation, out position);
            _cameraController.RotationEnabled = rotation;
            _cameraController.PositionEnabled = position;
        }

        /// <summary>
        /// The settings live in CameraUnlock.ini beside this DLL, in GoneHome_Data\Managed, read and
        /// written by core's config owner, with rows set to default following the player's
        /// Defaults.ini. While CameraUnlock.ini is absent the owner imports HeadTracking.cfg, the
        /// file every earlier build read, through the frozen v1.5.0 reader, and never writes it.
        /// Runs in Awake on the main thread, where the hotkeys that save also run.
        /// </summary>
        private void LoadConfig()
        {
            string dir = Path.GetDirectoryName(typeof(HeadTrackingMod).Assembly.Location);
            string configPath = Path.Combine(dir, GoneHomeConfig.FileName);
            _configOwner = new ConfigOwner<GoneHomeConfig>(new ConfigOwnerOptions<GoneHomeConfig>
            {
                Path = configPath,
                Table = GoneHomeConfig.Table(),
                Import = LegacyConfigImport.Create(),
                LegacySourcePath = Path.Combine(dir, GoneHomeConfig.LegacyFileName),
                Header = new RenderHeader(GoneHomeConfig.DisplayName),
                Defaults = DefaultsFile.PerUser(),
                // The mod draws no messages of its own, so the player's line goes to the log.
                StatusSink = message => Log("[Config] " + message),
            });

            ConfigLoadResult<GoneHomeConfig> loaded = _configOwner.Load();
            _config = loaded.Config;
            foreach (string line in loaded.Log) Log("[Config] " + line);
            Log("[Config] " + configPath + ": " + loaded.Status);

            _toggleKeys = ParseKeys("ToggleKey", _config.ToggleKeyName);
            _cycleTrackingModeKeys = ParseKeys("CycleTrackingModeKey", _config.CycleTrackingModeKeyName);
            _yawModeKeys = ParseKeys("YawModeKey", _config.YawModeKeyName);
        }

        // The table's hotkey codec has read every list the file holds, so a list that does not
        // parse reaches here only from a legacy import the owner deferred: a key code the key
        // table names no key for, which the import writes as the number. v1.5.0 still fired the
        // chord beside such a key, so the items that parse are bound and the rest are logged.
        private static KeyBinding[] ParseKeys(string key, string text)
        {
            KeyBinding[] bindings;
            string error;
            if (KeyBindings.TryParse(text, out bindings, out error)) return bindings;

            var kept = new List<KeyBinding>();
            foreach (string item in text.Split(','))
            {
                if (KeyBindings.TryParse(item, out bindings, out error)) kept.AddRange(bindings);
                else Log("[Config] [Hotkeys] " + key + ": " + error + ", so it is not bound this session");
            }
            return kept.ToArray();
        }

        /// <summary>
        /// Called after the new value is already applied. A save that fails is logged, the owner's
        /// reason reaches the log through the status sink, and the session keeps the new value.
        /// </summary>
        private void SaveConfig(Action<GoneHomeConfig> change)
        {
            ConfigSaveResult saved = _configOwner.Save(change);
            foreach (string line in saved.Log) Log("[Config] " + line);
            if (saved.Status != ConfigSaveStatus.Saved)
            {
                Log("[Config] " + saved.Status + ": the change applies to this session only.");
            }
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
            _reticleRenderer.ReticleColor = Color.white;
            _reticleRenderer.IsVisible = _isEnabled;

            // Hide the game's crosshair - we'll draw our own at the correct aim position.
            // With tracking off at start the game's crosshair stays until tracking is turned on.
            if (_isEnabled)
            {
                _gameReticleFinder.TryHideGameReticle();
            }

            // Update hook with aim components
            if (_cameraHook != null)
            {
                _cameraHook.SetAimComponents(_aimController, _gameReticleFinder, _interactionTextPositioner);
            }

            _aimSystemInitialized = true;
        }

        /// <summary>
        /// ReticlePositionProvider delegate for IMGUIReticle.
        /// Returns screen position for the reticle based on aim offset.
        /// </summary>
        private bool GetReticlePosition(out float screenX, out float screenY)
        {
            if (!CameraTrackingHook.IsInGameplay || _aimController == null)
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

        /// <summary>
        /// End and its chord: on and off for this session only. It never writes the config; the
        /// next start follows EnableOnStartup.
        /// </summary>
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
                    _reticleRenderer.IsVisible = true;
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

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            // Destroy camera hook if exists
            if (_cameraHook != null)
            {
                Destroy(_cameraHook);
                _cameraHook = null;
            }

            // During application quit Unity tears down GameObjects in an
            // arbitrary order, so the game's reticle / interaction-text objects
            // we cached may already be half-destroyed: the managed wrapper is
            // non-null but the native object is gone, and SetActive throws. We
            // only need to undo our scene-level changes when the mod is being
            // destroyed mid-session (scene change / recreate), not on quit.
            if (!_isQuitting)
            {
                _gameReticleFinder?.RestoreGameReticle();
                _interactionTextPositioner?.ResetPosition();
            }

            _receiver?.Dispose();
            _cameraController?.ResetCamera();
            Instance = null;

            // Schedule recreation on next frame
            if (!_isQuitting)
            {
                ModLoader.ScheduleRecreate();
            }
        }

        private static void Log(string message)
        {
            ModLoader.Log($"[Mod] {message}");
        }
    }
}
