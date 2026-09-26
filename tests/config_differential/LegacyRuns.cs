using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CameraUnlock.Core.Input;
using HeadTracking.Legacy;
using UnityEngine;

namespace HeadTracking.Tests.Differential
{
    /// <summary>What one run of a legacy reader gave: the settings, whether a file was there, and its log lines.</summary>
    internal sealed class LegacyOutcome
    {
        public LegacyConfig Config;
        public bool Found;
        public List<string> Log = new List<string>();

        public string Describe()
        {
            return "found=" + LegacyStartup.Text(Found) + "\n" + LegacyStartup.Fields(Config)
                   + string.Concat(Log.Select(l => "log: " + l + "\n"));
        }
    }

    /// <summary>
    /// What v1.5.0 ran on for one input, read by its own HeadTrackingConfig.LoadFromFile
    /// (oracle/HeadTrackingConfig.cs, the published file byte for byte) over core's
    /// ConfigParsingUtils as v1.5.0 pinned it (oracle/ConfigParsingUtils.cs). With no file it
    /// writes its default file into the scratch folder, as it did in game.
    /// </summary>
    internal static class Oracle
    {
        public static LegacyOutcome Run(DifferentialInput input)
        {
            using (var folder = new LegacyFolder(input))
            {
                var outcome = new LegacyOutcome { Found = input.Bytes != null };
                HeadTrackingConfig c = HeadTrackingConfig.LoadFromFile(folder.LegacyPath, outcome.Log.Add);
                outcome.Config = new LegacyConfig
                {
                    UdpPort = c.UdpPort,
                    YawSensitivity = c.YawSensitivity,
                    PitchSensitivity = c.PitchSensitivity,
                    RollSensitivity = c.RollSensitivity,
                    LocalSmoothing = c.LocalSmoothing,
                    RemoteSmoothing = c.RemoteSmoothing,
                    ToggleKey = c.ToggleKey,
                    PositionToggleKey = c.PositionToggleKey,
                    YawModeKey = c.YawModeKey,
                    WorldSpaceYaw = c.WorldSpaceYaw,
                    PositionSensitivityX = c.PositionSensitivityX,
                    PositionSensitivityY = c.PositionSensitivityY,
                    PositionSensitivityZ = c.PositionSensitivityZ,
                    InvertPositionX = c.InvertPositionX,
                    InvertPositionY = c.InvertPositionY,
                    InvertTrackerZ = c.InvertTrackerZ,
                    ShowReticle = c.ShowReticle,
                    ReticleColor = c.ReticleColor,
                };
                return outcome;
            }
        }
    }

    /// <summary>
    /// The frozen reader on one input. It must leave the file and its folder as they were. Its
    /// caller writes v1.5.0's default file for a missing one, so that log line is added here, where
    /// the oracle, which does both, writes it.
    /// </summary>
    internal static class FrozenReader
    {
        public const string CreatedLine = "Created default HeadTracking.cfg";

        public static LegacyOutcome Run(DifferentialInput input)
        {
            using (var folder = new LegacyFolder(input))
            {
                string[] before = folder.Entries();
                DateTime written = input.Bytes == null ? DateTime.MinValue : File.GetLastWriteTimeUtc(folder.LegacyPath);

                var outcome = new LegacyOutcome();
                string loadError;
                outcome.Config = LegacyConfigReader.Read(folder.LegacyPath, outcome.Log.Add, out outcome.Found, out loadError);
                if (loadError != null) throw new InvalidOperationException(input.Name + ": the reader failed: " + loadError);
                if (!outcome.Found) outcome.Log.Add(CreatedLine);

                if (!before.SequenceEqual(folder.Entries()))
                    throw new InvalidOperationException(input.Name + ": the frozen reader changed the folder: " + string.Join(", ", folder.Entries()));
                if (input.Bytes != null)
                {
                    if (!File.ReadAllBytes(folder.LegacyPath).SequenceEqual(input.Bytes))
                        throw new InvalidOperationException(input.Name + ": the frozen reader rewrote the legacy file");
                    if (File.GetLastWriteTimeUtc(folder.LegacyPath) != written)
                        throw new InvalidOperationException(input.Name + ": the frozen reader touched the legacy file");
                }
                return outcome;
            }
        }
    }

    /// <summary>
    /// What v1.5.0's HeadTrackingMod.Awake and Update set up from its settings, one line per item,
    /// floats with their bits.
    /// </summary>
    internal static class LegacyStartup
    {
        public static SortedDictionary<string, string> Of(LegacyConfig c)
        {
            var s = new SortedDictionary<string, string>(StringComparer.Ordinal);
            s["TrackingEnabled"] = "true";
            s["RotationEnabled"] = "true";
            s["PositionEnabled"] = "true";
            s["UdpPort"] = c.UdpPort.ToString(CultureInfo.InvariantCulture);
            s["WorldSpaceYaw"] = Text(c.WorldSpaceYaw);
            s["LocalSmoothing"] = Text(c.LocalSmoothing);
            s["RemoteSmoothing"] = Text(c.RemoteSmoothing);
            s["RotationSensitivity"] = Text(c.YawSensitivity) + " " + Text(c.PitchSensitivity) + " " + Text(c.RollSensitivity);
            s["PositionSensitivity"] = Text(c.PositionSensitivityX) + " " + Text(c.PositionSensitivityY) + " " + Text(c.PositionSensitivityZ);
            s["PositionInversion"] = Text(c.InvertPositionX) + " " + Text(c.InvertPositionY) + " " + Text(c.InvertTrackerZ);
            s["ReticleVisible"] = Text(c.ShowReticle);
            s["ReticleColor"] = Text(c.ReticleColor);
            s["ToggleKey"] = Hotkey(c.ToggleKey, KeyCode.Y);
            s["CycleTrackingModeKey"] = Hotkey(c.PositionToggleKey, KeyCode.G);
            s["YawModeKey"] = Hotkey(c.YawModeKey, KeyCode.H);
            return s;
        }

        /// <summary>Every field, floats with their bits.</summary>
        public static string Fields(LegacyConfig c)
        {
            var text = new StringBuilder();
            foreach (FieldInfo field in typeof(LegacyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(c);
                string shown = value is float ? Text((float)value)
                    : value is Color ? Text((Color)value)
                    : value is KeyCode ? ((int)(KeyCode)value).ToString(CultureInfo.InvariantCulture) + " " + value
                    : Convert.ToString(value, CultureInfo.InvariantCulture);
                text.Append(field.Name).Append('=').Append(shown).Append('\n');
            }
            return text.ToString();
        }

        public static string Text(bool value)
        {
            return value ? "true" : "false";
        }

        public static string Text(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture) + "/0x"
                   + BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("X8", CultureInfo.InvariantCulture);
        }

        public static string Text(Color value)
        {
            return Text(value.r) + " " + Text(value.g) + " " + Text(value.b) + " " + Text(value.a);
        }

        /// <summary>
        /// The bindings ChordHotkeys.IsActionPressed(primary, letter) fired on: the primary key
        /// whatever else was held, unless it was None, and Ctrl+Shift+letter.
        /// </summary>
        public static string Hotkey(KeyCode primary, KeyCode chordLetter)
        {
            string chord = KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
            if (primary == KeyCode.None) return chord;
            return KeyName((int)primary) + ", " + chord;
        }

        /// <summary>A Unity key code's name, or the number for one that names no key.</summary>
        public static string KeyName(int unityKeyCode)
        {
            try
            {
                return KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.None, unityKeyCode) });
            }
            catch (ArgumentException)
            {
                return unityKeyCode.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
