using System;
using System.Reflection;

namespace HeadTracking
{
    /// <summary>
    /// Centralized, search-once-cache-forever resolver for game types accessed via reflection.
    /// Three classes (CameraTrackingHook, GameReticleFinder, InteractionTextPositioner) all
    /// need types from game assemblies. This class scans AppDomain.GetAssemblies() once
    /// and caches the results.
    /// </summary>
    internal static class GameTypeResolver
    {
        private static bool _searched;

        // vp_FPSCamera — used by CameraTrackingHook for gameplay detection
        private static Type _fpsCameraType;

        // NGUI_HUD — used by GameReticleFinder and InteractionTextPositioner
        private static Type _nguiHudType;
        private static FieldInfo _reticuleSpriteField;
        private static PropertyInfo _nguiHudInstanceProperty;
        private static FieldInfo _focusLabelField;

        internal static Type FPSCameraType { get { EnsureSearched(); return _fpsCameraType; } }
        internal static Type NguiHudType { get { EnsureSearched(); return _nguiHudType; } }
        internal static FieldInfo ReticuleSpriteField { get { EnsureSearched(); return _reticuleSpriteField; } }
        internal static PropertyInfo NguiHudInstanceProperty { get { EnsureSearched(); return _nguiHudInstanceProperty; } }
        internal static FieldInfo FocusLabelField { get { EnsureSearched(); return _focusLabelField; } }

        private static void EnsureSearched()
        {
            if (_searched) return;
            _searched = true;

            bool foundFPS = false;
            bool foundHUD = false;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!foundFPS)
                    {
                        var t = asm.GetType("vp_FPSCamera");
                        if (NullHelper.NotNull(t))
                        {
                            _fpsCameraType = t;
                            foundFPS = true;
                        }
                    }

                    if (!foundHUD)
                    {
                        var t = asm.GetType("NGUI_HUD");
                        if (NullHelper.NotNull(t))
                        {
                            _nguiHudType = t;
                            foundHUD = true;

                            _reticuleSpriteField = t.GetField("ReticuleSprite",
                                BindingFlags.Public | BindingFlags.Instance);

                            _nguiHudInstanceProperty = t.GetProperty("instance",
                                BindingFlags.Public | BindingFlags.Static);

                            _focusLabelField = t.GetField("FocusLabel",
                                BindingFlags.Public | BindingFlags.Instance);
                        }
                    }

                    if (foundFPS && foundHUD) break;
                }
                catch { }
            }

            if (!foundFPS) ModLoader.Log("[GameTypeResolver] vp_FPSCamera type NOT found");
            if (!foundHUD) ModLoader.Log("[GameTypeResolver] NGUI_HUD type NOT found");
        }
    }
}
