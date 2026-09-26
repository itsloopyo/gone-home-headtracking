using CameraUnlock.Core.Config;

namespace HeadTracking
{
    /// <summary>
    /// Everything the mod reads from GoneHome_Data\Managed\CameraUnlock.ini. Unity-free, so the
    /// test project compiles it and holds the committed file to it.
    /// </summary>
    public sealed class GoneHomeConfig : HeadTrackingConfigData
    {
        /// <summary>The game's name as data/games.json spells it.</summary>
        public const string DisplayName = "Gone Home";

        public const string FileName = "CameraUnlock.ini";

        /// <summary>The file every published build read, beside CameraUnlock.ini.</summary>
        public const string LegacyFileName = "HeadTracking.cfg";

        public static ConfigTable<GoneHomeConfig> Table()
        {
            return HeadTrackingConfigTable.Create<GoneHomeConfig>(
                    ConfigConcepts.UdpPort,
                    ConfigConcepts.EnableOnStartup,
                    ConfigConcepts.WorldSpaceYaw,
                    ConfigConcepts.RotationEnabled,
                    ConfigConcepts.LocalSmoothing,
                    ConfigConcepts.RemoteSmoothing,
                    ConfigConcepts.PositionEnabled,
                    ConfigConcepts.ToggleKey,
                    ConfigConcepts.CycleTrackingModeKey,
                    ConfigConcepts.YawModeKey)
                .Select(ConfigConcepts.WorldSpaceYaw).Writable()
                .Select(ConfigConcepts.RotationEnabled).Writable()
                .Select(ConfigConcepts.PositionEnabled).Writable();
        }
    }
}
