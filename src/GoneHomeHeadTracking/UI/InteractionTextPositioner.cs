using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Moves the game's interaction text (NGUI_HUD.FocusLabel: "Open Door", "Examine") to follow
    /// the decoupled aim point instead of staying at screen center.
    /// </summary>
    public sealed class InteractionTextPositioner
    {
        private Transform _label;
        private Vector3 _homeLocalPosition;
        private bool _moved;

        /// <param name="screenOffset">Pixel offset of the aim point from screen center.</param>
        public void Follow(Component hud, Vector2 screenOffset)
        {
            Transform label = GameTypeResolver.FocusLabel(hud).transform;
            if (label != _label)
            {
                // A new HUD, or the first frame: the label sits where the game put it.
                _label = label;
                _homeLocalPosition = label.localPosition;
                _moved = false;
            }

            // Screen pixels map to the label's local units through the HUD camera and the UIRoot
            // scale above the label, which is 1:1 only when the UIRoot is pixel perfect.
            Camera hudCamera = GameTypeResolver.HudCamera(hud);
            Transform parent = label.parent;
            Vector3 screen = hudCamera.WorldToScreenPoint(parent.TransformPoint(_homeLocalPosition));
            screen.x += screenOffset.x;
            screen.y += screenOffset.y;
            Vector3 target = parent.InverseTransformPoint(hudCamera.ScreenToWorldPoint(screen));
            target.z = _homeLocalPosition.z;

            // An unchanged write would still mark the label dirty and rebuild its NGUI panel.
            if (label.localPosition != target)
            {
                label.localPosition = target;
                _moved = true;
            }
        }

        public void Restore()
        {
            if (!_moved) return;
            _moved = false;
            if (_label != null)
            {
                _label.localPosition = _homeLocalPosition;
            }
        }
    }
}
