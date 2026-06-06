using System;
using System.Collections.Generic;
using System.IO;

using CameraUnlock.Core.Config;
using CameraUnlock.Core.Protocol;
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

        // Smoothing
        public float Smoothing { get; set; } = 0.15f;

        // Hotkeys
        public KeyCode RecenterKey { get; set; } = KeyCode.Home;
        public KeyCode ToggleKey { get; set; } = KeyCode.End;
        public KeyCode PositionToggleKey { get; set; } = KeyCode.PageUp;
        public KeyCode YawModeKey { get; set; } = KeyCode.PageDown;

        // Yaw mode: false = camera-local yaw (default; matches dying-light-2 and obra-dinn),
        // true = horizon-locked yaw around world up (causes camera arc at extreme head yaw + mouse pitch).
        public bool WorldSpaceYaw { get; set; } = false;

        // Position tracking
        public float PositionSensitivityX { get; set; } = 1.0f;
        public float PositionSensitivityY { get; set; } = 1.0f;
        public float PositionSensitivityZ { get; set; } = 1.0f;
        public bool InvertPositionX { get; set; } = true;
        public bool InvertPositionY { get; set; } = false;
        public bool InvertPositionZ { get; set; } = true;

        // Aim decoupling
        public bool ShowReticle { get; set; } = true;
        public Color ReticleColor { get; set; } = Color.white;

        /// <summary>
        /// Loads configuration from file if it exists, otherwise returns defaults.
        /// </summary>
        /// <param name="configPath">Path to the config file</param>
        /// <param name="log">Optional logging action</param>
        /// <returns>Loaded or default configuration</returns>
        public static HeadTrackingConfig LoadFromFile(string configPath, Action<string> log = null)
        {
            var config = new HeadTrackingConfig();

            try
            {
                if (!File.Exists(configPath))
                {
                    WriteDefaults(configPath, log);
                    return config;
                }

                Dictionary<string, string> values = ConfigParsingUtils.ParseIniFile(configPath);
                foreach (var kvp in values)
                {
                    string key = kvp.Key.ToLowerInvariant();
                    string value = kvp.Value;

                    switch (key)
                    {
                        case "udpport":
                            if (ConfigParsingUtils.TryParseInt(value, out int port))
                                config.UdpPort = port;
                            break;
                        case "yawsensitivity":
                            if (ConfigParsingUtils.TryParseFloat(value, out float yaw))
                                config.YawSensitivity = yaw;
                            break;
                        case "pitchsensitivity":
                            if (ConfigParsingUtils.TryParseFloat(value, out float pitch))
                                config.PitchSensitivity = pitch;
                            break;
                        case "rollsensitivity":
                            if (ConfigParsingUtils.TryParseFloat(value, out float roll))
                                config.RollSensitivity = roll;
                            break;
                        case "smoothing":
                            if (ConfigParsingUtils.TryParseFloat(value, out float smoothing))
                                config.Smoothing = Math.Max(0f, Math.Min(1f, smoothing));
                            break;
                        case "recenterkey":
                            config.RecenterKey = ParseKeyCode(value, config.RecenterKey, "RecenterKey", log);
                            break;
                        case "togglekey":
                            config.ToggleKey = ParseKeyCode(value, config.ToggleKey, "ToggleKey", log);
                            break;
                        case "positiontogglekey":
                            config.PositionToggleKey = ParseKeyCode(value, config.PositionToggleKey, "PositionToggleKey", log);
                            break;
                        case "yawmodekey":
                            config.YawModeKey = ParseKeyCode(value, config.YawModeKey, "YawModeKey", log);
                            break;
                        case "worldspaceyaw":
                            if (ConfigParsingUtils.TryParseBool(value, out bool worldYaw))
                                config.WorldSpaceYaw = worldYaw;
                            break;
                        case "positionsensitivityx":
                            if (ConfigParsingUtils.TryParseFloat(value, out float posX))
                                config.PositionSensitivityX = posX;
                            break;
                        case "positionsensitivityy":
                            if (ConfigParsingUtils.TryParseFloat(value, out float posY))
                                config.PositionSensitivityY = posY;
                            break;
                        case "positionsensitivityz":
                            if (ConfigParsingUtils.TryParseFloat(value, out float posZ))
                                config.PositionSensitivityZ = posZ;
                            break;
                        case "invertpositionx":
                            if (ConfigParsingUtils.TryParseBool(value, out bool invX))
                                config.InvertPositionX = invX;
                            break;
                        case "invertpositiony":
                            if (ConfigParsingUtils.TryParseBool(value, out bool invY))
                                config.InvertPositionY = invY;
                            break;
                        case "invertpositionz":
                            if (ConfigParsingUtils.TryParseBool(value, out bool invZ))
                                config.InvertPositionZ = invZ;
                            break;
                        case "showreticle":
                            if (ConfigParsingUtils.TryParseBool(value, out bool show))
                                config.ShowReticle = show;
                            break;
                        case "reticlecolor":
                            if (ConfigParsingUtils.TryParseColor(value, out float[] rgba))
                                config.ReticleColor = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
                            break;
                    }
                }

                log?.Invoke("Config loaded from HeadTracking.cfg");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Config load error (using defaults): {ex.Message}");
            }

            return config;
        }

        private static KeyCode ParseKeyCode(string value, KeyCode fallback, string settingName, Action<string> log)
        {
            if (!Enum.IsDefined(typeof(KeyCode), value))
            {
                log?.Invoke($"Invalid {settingName} value '{value}' - using default {fallback}");
                return fallback;
            }
            return (KeyCode)Enum.Parse(typeof(KeyCode), value, true);
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
                    "RecenterKey = Home\n" +
                    "ToggleKey = End\n" +
                    "PositionToggleKey = PageUp\n" +
                    "YawModeKey = PageDown\n" +
                    "\n" +
                    "# --- Yaw Mode ---\n" +
                    "# false = camera-local yaw (default; matches dying-light-2/obra-dinn)\n" +
                    "# true = horizon-locked yaw around world up (causes camera arc at extreme head yaw + mouse pitch)\n" +
                    "WorldSpaceYaw = false\n" +
                    "\n" +
                    "# --- Sensitivity ---\n" +
                    "YawSensitivity = 1.0\n" +
                    "PitchSensitivity = 1.0\n" +
                    "RollSensitivity = 1.0\n" +
                    "\n" +
                    "# --- Smoothing ---\n" +
                    "# 0.0 = no smoothing, 1.0 = maximum.\n" +
                    "Smoothing = 0.15\n" +
                    "\n" +
                    "# --- Position Tracking ---\n" +
                    "PositionSensitivityX = 1.0\n" +
                    "PositionSensitivityY = 1.0\n" +
                    "PositionSensitivityZ = 1.0\n" +
                    "InvertPositionX = true\n" +
                    "InvertPositionY = false\n" +
                    "InvertPositionZ = true\n" +
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
