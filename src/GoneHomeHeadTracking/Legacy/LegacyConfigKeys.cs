using CameraUnlock.Core.Config;

namespace HeadTracking.Legacy
{
    /// <summary>
    /// Every key <see cref="LegacyConfigReader"/> reads. The reader ignores sections, so each key
    /// is read wherever it sits in the file. Frozen with it.
    /// </summary>
    internal static class LegacyConfigKeys
    {
        public static LegacyKey[] All()
        {
            return new[]
            {
                new LegacyKey("", "UdpPort"),
                new LegacyKey("", "YawSensitivity"),
                new LegacyKey("", "PitchSensitivity"),
                new LegacyKey("", "RollSensitivity"),
                new LegacyKey("", "LocalSmoothing"),
                new LegacyKey("", "RemoteSmoothing"),
                new LegacyKey("", "ToggleKey"),
                new LegacyKey("", "PositionToggleKey"),
                new LegacyKey("", "YawModeKey"),
                new LegacyKey("", "WorldSpaceYaw"),
                new LegacyKey("", "PositionSensitivityX"),
                new LegacyKey("", "PositionSensitivityY"),
                new LegacyKey("", "PositionSensitivityZ"),
                new LegacyKey("", "InvertPositionX"),
                new LegacyKey("", "InvertPositionY"),
                new LegacyKey("", "InvertTrackerZ"),
                new LegacyKey("", "ShowReticle"),
                new LegacyKey("", "ReticleColor"),
            };
        }
    }
}
