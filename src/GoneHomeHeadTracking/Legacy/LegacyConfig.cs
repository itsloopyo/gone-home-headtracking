using UnityEngine;

namespace HeadTracking.Legacy
{
    /// <summary>
    /// The settings v1.5.0 and every earlier build read from GoneHome_Data\Managed\HeadTracking.cfg,
    /// with v1.5.0's defaults. Frozen: a later change to the runtime settings or to core's constants
    /// must never change what an old file, or one missing a key, reads as, so the defaults are
    /// literals. v1.5.0 took the port from OpenTrackReceiver.DefaultPort, which held 4242.
    /// </summary>
    internal sealed class LegacyConfig
    {
        public int UdpPort = 4242;

        public float YawSensitivity = 1.0f;
        public float PitchSensitivity = 1.0f;
        public float RollSensitivity = 1.0f;

        public float LocalSmoothing = 0.0f;
        public float RemoteSmoothing = 0.15f;

        public KeyCode ToggleKey = KeyCode.End;
        public KeyCode PositionToggleKey = KeyCode.PageUp;
        public KeyCode YawModeKey = KeyCode.PageDown;

        public bool WorldSpaceYaw = true;

        public float PositionSensitivityX = 1.0f;
        public float PositionSensitivityY = 1.0f;
        public float PositionSensitivityZ = 1.0f;
        public bool InvertPositionX = true;
        public bool InvertPositionY = false;
        public bool InvertTrackerZ = false;

        public bool ShowReticle = true;
        public Color ReticleColor = Color.white;
    }
}
