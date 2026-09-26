using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HeadTracking.Legacy
{
    /// <summary>
    /// HeadTrackingConfig.LoadFromFile as v1.5.0 shipped it, frozen. It fills
    /// <see cref="LegacyConfig"/> and writes nothing: where v1.5.0 wrote its default file for a
    /// missing one, this returns the defaults with <c>found</c> false and leaves the write to the
    /// caller. The parse helpers it calls are <see cref="LegacyIniParsing"/>, core's as v1.5.0
    /// pinned them. The log lines are v1.5.0's.
    /// </summary>
    internal static class LegacyConfigReader
    {
        /// <param name="configPath">The legacy file.</param>
        /// <param name="log">Where v1.5.0 wrote its config lines, or null.</param>
        /// <param name="found">false when there is no file, which v1.5.0 answered with its defaults.</param>
        /// <param name="loadError">The message of the exception v1.5.0 caught while reading, after which
        /// it ran on its defaults; null when the read finished.</param>
        public static LegacyConfig Read(string configPath, Action<string> log, out bool found, out string loadError)
        {
            var config = new LegacyConfig();
            found = true;
            loadError = null;

            try
            {
                if (!File.Exists(configPath))
                {
                    found = false;
                    return config;
                }

                Dictionary<string, string> values = LegacyIniParsing.ParseIniFile(configPath);
                foreach (var kvp in values)
                {
                    string key = kvp.Key.ToLowerInvariant();
                    string value = kvp.Value;

                    switch (key)
                    {
                        case "udpport":
                            if (LegacyIniParsing.TryParseInt(value, out int port))
                                config.UdpPort = port;
                            break;
                        case "yawsensitivity":
                            if (LegacyIniParsing.TryParseFloat(value, out float yaw))
                                config.YawSensitivity = yaw;
                            break;
                        case "pitchsensitivity":
                            if (LegacyIniParsing.TryParseFloat(value, out float pitch))
                                config.PitchSensitivity = pitch;
                            break;
                        case "rollsensitivity":
                            if (LegacyIniParsing.TryParseFloat(value, out float roll))
                                config.RollSensitivity = roll;
                            break;
                        case "localsmoothing":
                            if (LegacyIniParsing.TryParseFloat(value, out float localSmoothing))
                                config.LocalSmoothing = Math.Max(0f, Math.Min(1f, localSmoothing));
                            break;
                        case "remotesmoothing":
                            if (LegacyIniParsing.TryParseFloat(value, out float remoteSmoothing))
                                config.RemoteSmoothing = Math.Max(0f, Math.Min(1f, remoteSmoothing));
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
                            if (LegacyIniParsing.TryParseBool(value, out bool worldYaw))
                                config.WorldSpaceYaw = worldYaw;
                            break;
                        case "positionsensitivityx":
                            if (LegacyIniParsing.TryParseFloat(value, out float posX))
                                config.PositionSensitivityX = posX;
                            break;
                        case "positionsensitivityy":
                            if (LegacyIniParsing.TryParseFloat(value, out float posY))
                                config.PositionSensitivityY = posY;
                            break;
                        case "positionsensitivityz":
                            if (LegacyIniParsing.TryParseFloat(value, out float posZ))
                                config.PositionSensitivityZ = posZ;
                            break;
                        case "invertpositionx":
                            if (LegacyIniParsing.TryParseBool(value, out bool invX))
                                config.InvertPositionX = invX;
                            break;
                        case "invertpositiony":
                            if (LegacyIniParsing.TryParseBool(value, out bool invY))
                                config.InvertPositionY = invY;
                            break;
                        case "inverttrackerz":
                            if (LegacyIniParsing.TryParseBool(value, out bool invZ))
                                config.InvertTrackerZ = invZ;
                            break;
                        case "showreticle":
                            if (LegacyIniParsing.TryParseBool(value, out bool show))
                                config.ShowReticle = show;
                            break;
                        case "reticlecolor":
                            if (LegacyIniParsing.TryParseColor(value, out float[] rgba))
                                config.ReticleColor = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
                            break;
                    }
                }

                log?.Invoke("Config loaded from HeadTracking.cfg");
            }
            catch (Exception ex)
            {
                loadError = ex.Message;
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
    }
}
