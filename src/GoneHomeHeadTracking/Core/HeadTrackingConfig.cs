using System;
using System.IO;

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
        public float Smoothing { get; set; } = 0.0f;

        // Hotkeys
        public KeyCode RecenterKey { get; set; } = KeyCode.Home;
        public KeyCode ToggleKey { get; set; } = KeyCode.End;
        public KeyCode PositionToggleKey { get; set; } = KeyCode.PageUp;

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

                foreach (string line in File.ReadAllLines(configPath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                        continue;

                    int eqIndex = trimmed.IndexOf('=');
                    if (eqIndex <= 0) continue;

                    string key = trimmed.Substring(0, eqIndex).Trim().ToLowerInvariant();
                    string value = trimmed.Substring(eqIndex + 1).Trim();

                    switch (key)
                    {
                        case "udpport":
                            if (int.TryParse(value, out int port))
                                config.UdpPort = port;
                            break;
                        case "yawsensitivity":
                            if (float.TryParse(value, out float yaw))
                                config.YawSensitivity = yaw;
                            break;
                        case "pitchsensitivity":
                            if (float.TryParse(value, out float pitch))
                                config.PitchSensitivity = pitch;
                            break;
                        case "rollsensitivity":
                            if (float.TryParse(value, out float roll))
                                config.RollSensitivity = roll;
                            break;
                        case "smoothing":
                            if (float.TryParse(value, out float smoothing))
                                config.Smoothing = Math.Max(0f, Math.Min(1f, smoothing));
                            break;
                        case "recenterkey":
                            if (!Enum.IsDefined(typeof(KeyCode), value))
                            {
                                log?.Invoke($"Invalid RecenterKey value '{value}' - using default {config.RecenterKey}");
                            }
                            else
                            {
                                config.RecenterKey = (KeyCode)Enum.Parse(typeof(KeyCode), value, true);
                            }
                            break;
                        case "togglekey":
                            if (!Enum.IsDefined(typeof(KeyCode), value))
                            {
                                log?.Invoke($"Invalid ToggleKey value '{value}' - using default {config.ToggleKey}");
                            }
                            else
                            {
                                config.ToggleKey = (KeyCode)Enum.Parse(typeof(KeyCode), value, true);
                            }
                            break;
                        case "positiontogglekey":
                            if (!Enum.IsDefined(typeof(KeyCode), value))
                            {
                                log?.Invoke($"Invalid PositionToggleKey value '{value}' - using default {config.PositionToggleKey}");
                            }
                            else
                            {
                                config.PositionToggleKey = (KeyCode)Enum.Parse(typeof(KeyCode), value, true);
                            }
                            break;
                        case "positionsensitivityx":
                            if (float.TryParse(value, out float posX))
                                config.PositionSensitivityX = posX;
                            break;
                        case "positionsensitivityy":
                            if (float.TryParse(value, out float posY))
                                config.PositionSensitivityY = posY;
                            break;
                        case "positionsensitivityz":
                            if (float.TryParse(value, out float posZ))
                                config.PositionSensitivityZ = posZ;
                            break;
                        case "invertpositionx":
                            if (bool.TryParse(value, out bool invX))
                                config.InvertPositionX = invX;
                            break;
                        case "invertpositiony":
                            if (bool.TryParse(value, out bool invY))
                                config.InvertPositionY = invY;
                            break;
                        case "invertpositionz":
                            if (bool.TryParse(value, out bool invZ))
                                config.InvertPositionZ = invZ;
                            break;
                        case "showreticle":
                            if (bool.TryParse(value, out bool show))
                                config.ShowReticle = show;
                            break;
                        case "reticlecolor":
                            config.ReticleColor = ParseColor(value);
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
                    "\n" +
                    "# --- Sensitivity ---\n" +
                    "YawSensitivity = 1.0\n" +
                    "PitchSensitivity = 1.0\n" +
                    "RollSensitivity = 1.0\n" +
                    "\n" +
                    "# --- Smoothing ---\n" +
                    "# 0.0 = no smoothing, 1.0 = maximum. Remote connections enforce a minimum of 0.15.\n" +
                    "Smoothing = 0.0\n" +
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
        /// Parses color from R,G,B,A format (e.g., "1.0,1.0,1.0,0.8")
        /// </summary>
        private static Color ParseColor(string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length < 3)
                return Color.white;

            float r = 1f, g = 1f, b = 1f, a = 1f;
            if (float.TryParse(parts[0].Trim(), out float parsedR)) r = parsedR;
            if (float.TryParse(parts[1].Trim(), out float parsedG)) g = parsedG;
            if (float.TryParse(parts[2].Trim(), out float parsedB)) b = parsedB;
            if (parts.Length >= 4 && float.TryParse(parts[3].Trim(), out float parsedA)) a = parsedA;

            return new Color(r, g, b, a);
        }

        /// <summary>
        /// Gets the default config file path next to the assembly.
        /// </summary>
        public static string GetDefaultConfigPath()
        {
            string assemblyDir = Path.GetDirectoryName(typeof(HeadTrackingConfig).Assembly.Location);
            return Path.Combine(assemblyDir ?? "", "HeadTracking.cfg");
        }
    }
}
