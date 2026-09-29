using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using CameraUnlock.Core.Reflection;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Centralized, search-once-cache-forever resolver for game types accessed via reflection.
    /// Members read every frame go through compiled getters, so the per-frame reads neither
    /// box nor pay FieldInfo.GetValue.
    /// </summary>
    internal static class GameTypeResolver
    {
        private static bool _searched;

        private static Type _fpsCameraType;
        private static Type _frobManagerType;

        private static Func<object, int> _frobLayerMask;
        private static Func<object, float> _maxFrobDistance;

        private static Func<bool> _hudIsActive;
        private static Func<Component> _hudInstance;
        private static Func<object, bool> _hudShowReticule;
        private static Func<object, Component> _hudReticuleSprite;
        private static Func<object, Component> _hudFocusLabel;
        private static Func<object, Camera> _hudCamera;

        internal static Type FPSCameraType { get { EnsureSearched(); return _fpsCameraType; } }
        internal static Type FrobManagerType { get { EnsureSearched(); return _frobManagerType; } }

        /// <summary>True when FrobManager and both members the interaction ray uses resolved.</summary>
        internal static bool HasFrobRay { get { EnsureSearched(); return _frobLayerMask != null && _maxFrobDistance != null; } }

        /// <summary>True when NGUI_HUD and every member the mod reads off it resolved.</summary>
        internal static bool HasHud { get { EnsureSearched(); return _hudFocusLabel != null; } }

        internal static int FrobLayerMask(Component frobManager) => _frobLayerMask(frobManager);
        internal static float MaxFrobDistance(Component frobManager) => _maxFrobDistance(frobManager);

        /// <summary>
        /// The live NGUI_HUD, or null when the scene has none. NGUI_HUD.instance falls back to
        /// FindObjectOfType and logs a warning whenever no HUD is registered, so isActive is
        /// read first.
        /// </summary>
        internal static Component CurrentHud()
        {
            if (!HasHud || !_hudIsActive()) return null;
            return _hudInstance();
        }

        internal static bool ShowReticule(Component hud) => _hudShowReticule(hud);
        internal static Component ReticuleSprite(Component hud) => _hudReticuleSprite(hud);
        internal static Component FocusLabel(Component hud) => _hudFocusLabel(hud);
        internal static Camera HudCamera(Component hud) => _hudCamera(hud);

        private static void EnsureSearched()
        {
            if (_searched) return;
            _searched = true;

            _fpsCameraType = FindTypeByName("vp_FPSCamera");
            if (NullHelper.IsNull(_fpsCameraType)) ModLoader.Log("[GameTypeResolver] vp_FPSCamera type NOT found");

            _frobManagerType = FindTypeByName("FrobManager");
            if (NullHelper.NotNull(_frobManagerType))
            {
                FieldInfo mask = _frobManagerType.GetField("FrobLayerMask", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo distance = _frobManagerType.GetField("MaxFrobDistance", BindingFlags.Public | BindingFlags.Instance);
                if (NullHelper.NotNull(mask) && NullHelper.NotNull(distance))
                {
                    // LayerMask converts to int through its implicit operator.
                    _frobLayerMask = CompiledGetters.ForInstanceField<int>(mask);
                    _maxFrobDistance = CompiledGetters.ForInstanceField<float>(distance);
                }
                else
                {
                    ModLoader.Log("[GameTypeResolver] FrobManager.FrobLayerMask or MaxFrobDistance NOT found");
                }
            }
            else
            {
                ModLoader.Log("[GameTypeResolver] FrobManager type NOT found");
            }

            Type hud = FindTypeByName("NGUI_HUD");
            if (NullHelper.IsNull(hud))
            {
                ModLoader.Log("[GameTypeResolver] NGUI_HUD type NOT found");
                return;
            }

            PropertyInfo isActive = hud.GetProperty("isActive", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo instance = hud.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo showReticule = hud.GetProperty("ShowReticule", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo reticuleSprite = hud.GetField("ReticuleSprite", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo focusLabel = hud.GetField("FocusLabel", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo hudCamera = hud.GetField("HUDCamera", BindingFlags.Public | BindingFlags.Instance);
            if (NullHelper.IsNull(isActive) || NullHelper.IsNull(instance) || NullHelper.IsNull(showReticule)
                || NullHelper.IsNull(reticuleSprite) || NullHelper.IsNull(focusLabel) || NullHelper.IsNull(hudCamera))
            {
                ModLoader.Log("[GameTypeResolver] NGUI_HUD is missing isActive, instance, ShowReticule, ReticuleSprite, FocusLabel or HUDCamera");
                return;
            }

            _hudIsActive = CompiledGetters.ForStaticProperty<bool>(isActive);
            _hudInstance = CompiledGetters.ForStaticProperty<Component>(instance);
            _hudShowReticule = ForInstanceProperty<bool>(showReticule);
            _hudReticuleSprite = CompiledGetters.ForInstanceField<Component>(reticuleSprite);
            _hudCamera = CompiledGetters.ForInstanceField<Camera>(hudCamera);
            _hudFocusLabel = CompiledGetters.ForInstanceField<Component>(focusLabel);
        }

        private static Func<object, T> ForInstanceProperty<T>(PropertyInfo property)
        {
            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            Expression typedInstance = Expression.Convert(instance, property.DeclaringType);
            Expression body = Expression.Convert(Expression.Property(typedInstance, property), typeof(T));
            return Expression.Lambda<Func<object, T>>(body, instance).Compile();
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
