using System;
using System.IO;

using CameraUnlock.Core.Config;
using CameraUnlock.Core.Protocol;
using HeadTracking.Legacy;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Configuration for head tracking mod.
    /// Loaded from HeadTracking.cfg file if present.
    /// </summary>
    public sealed class HeadTrackingConfig
    {
        // Network
        public int UdpPort { get; set; } = OpenTrackReceiver.DefaultPort;

        // Sensitivity
        public float YawSensitivity { get; set; } = 1.0f;
        public float PitchSensitivity { get; set; } = 1.0f;
        public float RollSensitivity { get; set; } = 1.0f;

        // Smoothing. Selected per connection from the tracker's source address:
        // a tracker on this machine uses LocalSmoothing, a remote network device
        // uses RemoteSmoothing. Both cover rotation and position.
        public float LocalSmoothing { get; set; } = CameraUnlock.Core.Math.SmoothingUtils.DefaultLocalSmoothing;
        public float RemoteSmoothing { get; set; } = CameraUnlock.Core.Math.SmoothingUtils.DefaultRemoteSmoothing;

        // Hotkeys
        public KeyCode ToggleKey { get; set; } = KeyCode.End;
        public KeyCode PositionToggleKey { get; set; } = KeyCode.PageUp;
        public KeyCode YawModeKey { get; set; } = KeyCode.PageDown;

        // Yaw mode: true = horizon-locked yaw around world up (default),
        // false = camera-local yaw (matches dying-light-2 and obra-dinn).
        public bool WorldSpaceYaw { get; set; } = true;

        // Position tracking
        public float PositionSensitivityX { get; set; } = 1.0f;
        public float PositionSensitivityY { get; set; } = 1.0f;
        public float PositionSensitivityZ { get; set; } = 1.0f;
        public bool InvertPositionX { get; set; } = true;
        public bool InvertPositionY { get; set; } = false;
        /// Renamed from InvertPositionZ, which every existing HeadTracking.cfg carries as
        /// true. It used to double as the flip into Unity's +z-forward world space, a job
        /// cameraunlock-core now does at the engine boundary; left in place it would invert
        /// the lean. The key has to change so existing files fall back to this default.
        public bool InvertTrackerZ { get; set; } = false;

        // Aim decoupling
        public bool ShowReticle { get; set; } = true;
        public Color ReticleColor { get; set; } = Color.white;

        /// <summary>
        /// Loads configuration from file if it exists, otherwise writes the default file and
        /// returns defaults. The file is read by the frozen v1.5.0 reader in Legacy/.
        /// </summary>
        /// <param name="configPath">Path to the config file</param>
        /// <param name="log">Optional logging action</param>
        /// <returns>Loaded or default configuration</returns>
        public static HeadTrackingConfig LoadFromFile(string configPath, Action<string> log = null)
        {
            LegacyConfig legacy = LegacyConfigReader.Read(configPath, log, out bool found, out _);
            if (!found)
            {
                WriteDefaults(configPath, log);
            }

            return new HeadTrackingConfig
            {
                UdpPort = legacy.UdpPort,
                YawSensitivity = legacy.YawSensitivity,
                PitchSensitivity = legacy.PitchSensitivity,
                RollSensitivity = legacy.RollSensitivity,
                LocalSmoothing = legacy.LocalSmoothing,
                RemoteSmoothing = legacy.RemoteSmoothing,
                ToggleKey = legacy.ToggleKey,
                PositionToggleKey = legacy.PositionToggleKey,
                YawModeKey = legacy.YawModeKey,
                WorldSpaceYaw = legacy.WorldSpaceYaw,
                PositionSensitivityX = legacy.PositionSensitivityX,
                PositionSensitivityY = legacy.PositionSensitivityY,
                PositionSensitivityZ = legacy.PositionSensitivityZ,
                InvertPositionX = legacy.InvertPositionX,
                InvertPositionY = legacy.InvertPositionY,
                InvertTrackerZ = legacy.InvertTrackerZ,
                ShowReticle = legacy.ShowReticle,
                ReticleColor = legacy.ReticleColor,
            };
        }

        private static void WriteDefaults(string configPath, Action<string> log)
        {
            try
            {
                File.WriteAllText(configPath,
                    "# Gone Home Head Tracking Configuration\n" +
                    "# Edit values below and restart the game to apply changes.\n" +
                    "# Lines starting with # or ; are comments.\n" +
                    "\n" +
                    "# --- Network ---\n" +
                    "UdpPort = 4242\n" +
                    "\n" +
                    "# --- Keybindings ---\n" +
                    "# See https://docs.unity3d.com/ScriptReference/KeyCode.html for key names\n" +
                    "ToggleKey = End\n" +
                    "PositionToggleKey = PageUp\n" +
                    "YawModeKey = PageDown\n" +
                    "\n" +
                    "# --- Yaw Mode ---\n" +
                    "# true = horizon-locked yaw around world up (default)\n" +
                    "# false = camera-local yaw (matches dying-light-2/obra-dinn)\n" +
                    "WorldSpaceYaw = true\n" +
                    "\n" +
                    "# --- Sensitivity ---\n" +
                    "YawSensitivity = 1.0\n" +
                    "PitchSensitivity = 1.0\n" +
                    "RollSensitivity = 1.0\n" +
                    "\n" +
                    "# --- Smoothing ---\n" +
                    "# Picked per connection from the tracker's source address. Both values\n" +
                    "# cover rotation and position. 0.0 = no smoothing, 1.0 = heavy.\n" +
                    "# LocalSmoothing: tracker running on this machine (loopback).\n" +
                    "# RemoteSmoothing: tracker on a remote device over the network.\n" +
                    "LocalSmoothing = 0.0\n" +
                    "RemoteSmoothing = 0.15\n" +
                    "\n" +
                    "# --- Position Tracking ---\n" +
                    "PositionSensitivityX = 1.0\n" +
                    "PositionSensitivityY = 1.0\n" +
                    "PositionSensitivityZ = 1.0\n" +
                    "InvertPositionX = true\n" +
                    "InvertPositionY = false\n" +
                    "InvertTrackerZ = false\n" +
                    "\n" +
                    "# --- Reticle ---\n" +
                    "ShowReticle = true\n" +
                    "ReticleColor = 1.0,1.0,1.0,1.0\n");
                log?.Invoke("Created default HeadTracking.cfg");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Could not create default config: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the default config file path next to the assembly.
        /// </summary>
        public static string GetDefaultConfigPath()
        {
            string assemblyDir = ConfigParsingUtils.GetAssemblyDirectory(typeof(HeadTrackingConfig).Assembly);
            return Path.Combine(assemblyDir, "HeadTracking.cfg");
        }
    }
}
