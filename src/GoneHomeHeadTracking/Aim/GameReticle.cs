using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Hides the game's crosshair (NGUI_HUD.ReticuleSprite) while the mod draws its own at the
    /// aim point, and hands it back to the game's own visibility state when tracking stops.
    /// </summary>
    public sealed class GameReticle
    {
        private Component _hud;
        private GameObject _hidden;

        /// <summary>
        /// Hides the game's crosshair for this frame and returns whether the game wants one shown.
        /// The game switches it per input context (NGUI_HUD.ShowReticule), which re-activates the
        /// sprite, so this runs every tracked frame.
        /// </summary>
        public bool Hide(Component hud)
        {
            _hud = hud;
            _hidden = GameTypeResolver.ReticuleSprite(hud).gameObject;
            if (_hidden.activeSelf)
            {
                _hidden.SetActive(false);
            }
            return GameTypeResolver.ShowReticule(hud);
        }

        public void Restore()
        {
            // Unity's == also catches a HUD destroyed with its sprite, which needs nothing back.
            if (_hidden != null && _hud != null)
            {
                _hidden.SetActive(GameTypeResolver.ShowReticule(_hud));
            }
            _hidden = null;
            _hud = null;
        }
    }
}
