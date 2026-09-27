using System;
using System.Collections.Generic;
using System.Text;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using UnityEngine;

namespace HeadTracking.Legacy
{
    /// <summary>
    /// The import the config owner runs on HeadTracking.cfg while CameraUnlock.ini is absent:
    /// <see cref="LegacyConfigReader"/>, then the map into <see cref="GoneHomeConfig"/>.
    /// </summary>
    internal static class LegacyConfigImport
    {
        /// <summary>The rotation and position multiplier every published build shipped on every axis.</summary>
        public const float ShippedSensitivity = 1.0f;

        /// <summary>
        /// The position axis flips every published build shipped: x inverted, y and the tracker's z
        /// not. They are the mod's axis conversion, which the mod now applies in code.
        /// </summary>
        public const bool ShippedInvertPositionX = true;
        public const bool ShippedInvertPositionY = false;
        public const bool ShippedInvertTrackerZ = false;

        public static LegacyImport<GoneHomeConfig> Create()
        {
            return new LegacyImport<GoneHomeConfig>(Run, LegacyConfigKeys.All());
        }

        /// <summary>
        /// A file v1.5.0 failed to read gave it its defaults for the session. The import refuses it
        /// with v1.5.0's reason, so this session runs on those defaults, as v1.5.0 did, nothing is
        /// written, and the next start tries again.
        /// </summary>
        public static ImportResult Run(LegacyImportInput input, GoneHomeConfig config)
        {
            bool found;
            string loadError;
            LegacyConfig legacy = LegacyConfigReader.Read(input.Path, null, out found, out loadError);
            var dropped = new List<DroppedValue>();
            var poseShaping = new List<PoseShapingValue>();
            var followsDefaultsIni = new LegacyFollowsDefaultsIni();
            Map(legacy, config, dropped, poseShaping, followsDefaultsIni);
            if (loadError != null) return ImportResult.Refused("Config load error (using defaults): " + loadError);
            return found
                ? ImportResult.Imported(dropped, poseShaping, followsDefaultsIni.Concepts)
                : ImportResult.Absent(dropped, poseShaping, followsDefaultsIni.Concepts);
        }

        /// <summary>
        /// The reader refuses NaN and infinity, so no value reaches here that normalisation N2 would
        /// change. The file has no sections, so a dropped value names none. Every row is compared
        /// with v1.5.0's own default, a fresh <see cref="LegacyConfig"/>, so a setting the player
        /// never changed follows Defaults.ini.
        /// </summary>
        public static void Map(LegacyConfig legacy, GoneHomeConfig config, List<DroppedValue> dropped,
            List<PoseShapingValue> poseShaping, LegacyFollowsDefaultsIni followsDefaultsIni)
        {
            var shipped = new LegacyConfig();

            config.UdpPort = legacy.UdpPort;
            followsDefaultsIni.Setting(ConfigConcepts.UdpPort, legacy.UdpPort, shipped.UdpPort);

            // Every published build started with head tracking on and in rotation and position, and
            // had no setting for either.
            config.EnableOnStartup = true;
            followsDefaultsIni.NotInLegacy(ConfigConcepts.EnableOnStartup);
            config.RotationEnabled = true;
            config.PositionEnabled = true;
            followsDefaultsIni.TrackingMode(true);

            config.WorldSpaceYaw = legacy.WorldSpaceYaw;
            followsDefaultsIni.Setting(ConfigConcepts.WorldSpaceYaw, legacy.WorldSpaceYaw, shipped.WorldSpaceYaw);

            config.ToggleKeyName = HotkeyList(legacy.ToggleKey, KeyCode.Y, "ToggleKey", dropped);
            followsDefaultsIni.Setting(ConfigConcepts.ToggleKey, legacy.ToggleKey, shipped.ToggleKey);
            config.CycleTrackingModeKeyName = HotkeyList(legacy.PositionToggleKey, KeyCode.G, "PositionToggleKey", dropped);
            followsDefaultsIni.Setting(ConfigConcepts.CycleTrackingModeKey, legacy.PositionToggleKey, shipped.PositionToggleKey);
            config.YawModeKeyName = HotkeyList(legacy.YawModeKey, KeyCode.H, "YawModeKey", dropped);
            followsDefaultsIni.Setting(ConfigConcepts.YawModeKey, legacy.YawModeKey, shipped.YawModeKey);

            config.LocalSmoothing = legacy.LocalSmoothing;
            followsDefaultsIni.Setting(ConfigConcepts.LocalSmoothing, legacy.LocalSmoothing, shipped.LocalSmoothing);
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            followsDefaultsIni.Setting(ConfigConcepts.RemoteSmoothing, legacy.RemoteSmoothing, shipped.RemoteSmoothing);
            PositionSettings p = config.Position;
            config.Position = new PositionSettings(
                p.SensitivityX, p.SensitivityY, p.SensitivityZ,
                p.LimitX, p.LimitY, p.LimitYDown, p.LimitZ, p.LimitZBack,
                legacy.LocalSmoothing, legacy.RemoteSmoothing,
                p.InvertX, p.InvertY, p.InvertZ);

            LegacyPoseShaping.Record(legacy.YawSensitivity, ShippedSensitivity, "", "YawSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchSensitivity, ShippedSensitivity, "", "PitchSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollSensitivity, ShippedSensitivity, "", "RollSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityX, ShippedSensitivity, "", "PositionSensitivityX", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityY, ShippedSensitivity, "", "PositionSensitivityY", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityZ, ShippedSensitivity, "", "PositionSensitivityZ", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertPositionX, ShippedInvertPositionX, "", "InvertPositionX", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertPositionY, ShippedInvertPositionY, "", "InvertPositionY", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertTrackerZ, ShippedInvertTrackerZ, "", "InvertTrackerZ", poseShaping, dropped);

            // The reticle has no settings now. ShowReticle=true and white are what it still does, so
            // only a player who had hidden or recoloured it loses a choice.
            if (!legacy.ShowReticle)
            {
                dropped.Add(new DroppedValue(DropRule.Reticle, "", "ShowReticle", "false"));
            }
            Color c = legacy.ReticleColor;
            if (c.r != 1f || c.g != 1f || c.b != 1f || c.a != 1f)
            {
                string text = Encoding.ASCII.GetString(new ColorCodec().Render(new[] { c.r, c.g, c.b, c.a }));
                dropped.Add(new DroppedValue(DropRule.Reticle, "", "ReticleColor", text));
            }
        }

        /// <summary>
        /// The keys v1.5.0 fired an action on: the configured key, unless it was None, and the
        /// Ctrl+Shift chord that ChordHotkeys checked beside it. A Ctrl, Shift or Alt key on its own
        /// is left unbound and recorded under <paramref name="legacyKey"/> (normalisation N3), and
        /// the chord stays.
        /// </summary>
        public static string HotkeyList(KeyCode primary, KeyCode chordLetter, string legacyKey, List<DroppedValue> dropped)
        {
            string chord = KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
            string key = LegacyNormalisations.KeyCodeToBindings((int)primary, "", legacyKey, dropped);
            return key.Length == 0 ? chord : key + ", " + chord;
        }
    }
}
