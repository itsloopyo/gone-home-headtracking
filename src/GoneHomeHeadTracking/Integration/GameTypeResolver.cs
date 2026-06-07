using System;
using System.IO;
using System.Reflection;

namespace HeadTracking
{
    /// <summary>
    /// Centralized, search-once-cache-forever resolver for game types accessed via reflection.
    /// Three classes (CameraTrackingHook, GameReticleFinder, InteractionTextPositioner) all
    /// need types from game assemblies. This class resolves them once and caches the results.
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

            _fpsCameraType = FindTypeByName("vp_FPSCamera");
            _nguiHudType = FindTypeByName("NGUI_HUD");

            if (NullHelper.NotNull(_nguiHudType))
            {
                _reticuleSpriteField = _nguiHudType.GetField("ReticuleSprite",
                    BindingFlags.Public | BindingFlags.Instance);

                _nguiHudInstanceProperty = _nguiHudType.GetProperty("instance",
                    BindingFlags.Public | BindingFlags.Static);

                _focusLabelField = _nguiHudType.GetField("FocusLabel",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            if (NullHelper.IsNull(_fpsCameraType)) ModLoader.Log("[GameTypeResolver] vp_FPSCamera type NOT found");
            if (NullHelper.IsNull(_nguiHudType)) ModLoader.Log("[GameTypeResolver] NGUI_HUD type NOT found");
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
                catch (ReflectionTypeLoadException) { }
                catch (FileNotFoundException) { }
            }
            return null;
        }
    }
}
