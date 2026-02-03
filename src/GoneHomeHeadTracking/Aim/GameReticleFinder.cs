using System.Reflection;
using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Finds and hides the game's original crosshair/reticle.
    /// Gone Home uses NGUI_HUD.ReticuleSprite (UISprite), not vp_FPSCrosshair.
    /// </summary>
    public sealed class GameReticleFinder
    {
        private MonoBehaviour _hudInstance;
        private GameObject _reticuleGameObject;
        private bool _wasActive;

        // Throttle expensive FindObjectsOfTypeAll calls during HUD search
        private int _findRetryCounter;
        private const int FindRetryInterval = 30; // ~0.5s at 60fps

        /// <summary>
        /// Call every frame to keep the game's reticle hidden.
        /// </summary>
        public void TryHideGameReticle()
        {
            if (NullHelper.IsNull(GameTypeResolver.NguiHudType))
                return;

            // Find HUD instance if we don't have one (throttled to avoid per-frame FindObjectsOfTypeAll)
            if (NullHelper.IsNull(_hudInstance) || _hudInstance == null)
            {
                if (_findRetryCounter > 0)
                {
                    _findRetryCounter--;
                    return;
                }
                _findRetryCounter = FindRetryInterval;
                FindHUDInstance();
            }

            // Hide reticle if we have a reference (re-hide after restore)
            if (NullHelper.NotNull(_reticuleGameObject) && _reticuleGameObject != null)
            {
                if (_reticuleGameObject.activeSelf)
                {
                    _reticuleGameObject.SetActive(false);
                }
            }
        }

        private void FindHUDInstance()
        {
            var hudType = GameTypeResolver.NguiHudType;
            var reticuleSpriteField = GameTypeResolver.ReticuleSpriteField;

            try
            {
                // Method 1: FindObjectsOfTypeAll
                var allHuds = Resources.FindObjectsOfTypeAll(hudType);

                foreach (var obj in allHuds)
                {
                    var mb = obj as MonoBehaviour;
                    if (NullHelper.IsNull(mb)) continue;

                    _hudInstance = mb;
                    if (TryHideReticleSprite(reticuleSpriteField))
                        return;
                }

                // Method 2: Try static instance property
                var instanceProp = GameTypeResolver.NguiHudInstanceProperty;
                if (NullHelper.NotNull(instanceProp))
                {
                    var instance = instanceProp.GetValue(null, null) as MonoBehaviour;
                    if (NullHelper.NotNull(instance))
                    {
                        _hudInstance = instance;
                        if (TryHideReticleSprite(reticuleSpriteField))
                            return;
                    }
                }
            }
            catch
            {
            }
        }

        private bool TryHideReticleSprite(FieldInfo reticuleSpriteField)
        {
            if (NullHelper.IsNull(reticuleSpriteField))
                return false;

            var sprite = reticuleSpriteField.GetValue(_hudInstance);
            if (NullHelper.IsNull(sprite))
                return false;

            var spriteComp = sprite as Component;
            if (NullHelper.IsNull(spriteComp))
                return false;

            _reticuleGameObject = spriteComp.gameObject;
            _wasActive = _reticuleGameObject.activeSelf;
            _reticuleGameObject.SetActive(false);
            return true;
        }

        public void RestoreGameReticle()
        {
            if (NullHelper.NotNull(_reticuleGameObject) && _reticuleGameObject != null)
            {
                _reticuleGameObject.SetActive(_wasActive);
            }
        }
    }
}
