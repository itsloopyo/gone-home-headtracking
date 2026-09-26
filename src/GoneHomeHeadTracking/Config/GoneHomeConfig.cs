using System;
using CameraUnlock.Core.Config;
using HeadTracking.Legacy;

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

        /// <summary>
        /// The owner's options for the CameraUnlock.ini in <paramref name="folder"/>, importing the
        /// HeadTracking.cfg beside it. The mod passes <see cref="DefaultsFile.PerUser"/> and every
        /// test a <see cref="DefaultsFile.At"/> scratch path, so both run the options the mod ships.
        /// </summary>
        public static ConfigOwnerOptions<GoneHomeConfig> OwnerOptions(string folder, DefaultsFile defaults, Action<string> statusSink = null)
        {
            return new ConfigOwnerOptions<GoneHomeConfig>
            {
                Path = System.IO.Path.Combine(folder, FileName),
                Table = Table(),
                Import = LegacyConfigImport.Create(),
                LegacySourcePath = System.IO.Path.Combine(folder, LegacyFileName),
                Header = new RenderHeader(DisplayName),
                Defaults = defaults,
                StatusSink = statusSink,
            };
        }
    }
}
