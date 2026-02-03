using UnityEngine;

namespace HeadTracking
{
    /// <summary>
    /// Repositions the game's interaction text (e.g., "Open Door", "Examine")
    /// to follow the decoupled aim point instead of staying at screen center.
    ///
    /// Gone Home uses NGUI for its HUD. The interaction text is displayed via:
    /// - NGUI_HUD.instance.FocusLabel (a UILabel component)
    ///
    /// UILabel inherits from UIWidget which uses Transform.localPosition for positioning.
    /// NGUI coordinates are screen-space with origin typically at center.
    /// </summary>
    public sealed class InteractionTextPositioner
    {
        // Cached UI elements
        private object _focusLabelInstance;
        private Transform _focusLabelTransform;
        private Vector3 _originalLocalPosition;

        // State
        private bool _elementsSearched;
        private bool _initialized;

        /// <summary>
        /// Updates the position of interaction UI elements based on the aim offset.
        /// Call this every frame after AimController.UpdateAim().
        /// </summary>
        /// <param name="screenOffset">Pixel offset from screen center where aim point is</param>
        public void UpdatePosition(Vector2 screenOffset)
        {
            // Lazy initialization - find elements if we haven't yet
            if (!_elementsSearched)
            {
                FindUIElements();
            }

            // If we still don't have the elements, nothing to do
            if (!_initialized || NullHelper.IsNull(_focusLabelTransform))
            {
                return;
            }

            // NGUI uses local position for UI elements
            // Assume 1:1 pixel mapping (common for NGUI setups at native resolution)
            Vector3 newPos = _originalLocalPosition;
            newPos.x += screenOffset.x;
            newPos.y += screenOffset.y;

            _focusLabelTransform.localPosition = newPos;
        }

        /// <summary>
        /// Resets interaction UI elements to their original positions.
        /// Call when tracking is disabled or disconnected.
        /// </summary>
        public void ResetPosition()
        {
            if (!NullHelper.IsNull(_focusLabelTransform))
            {
                _focusLabelTransform.localPosition = _originalLocalPosition;
            }
        }

        private void FindUIElements()
        {
            _elementsSearched = true;

            var instanceProperty = GameTypeResolver.NguiHudInstanceProperty;
            var focusLabelField = GameTypeResolver.FocusLabelField;

            if (NullHelper.IsNull(GameTypeResolver.NguiHudType) || NullHelper.IsNull(instanceProperty) || NullHelper.IsNull(focusLabelField))
            {
                return;
            }

            try
            {
                // Get the NGUI_HUD singleton instance
                var hudInstance = instanceProperty.GetValue(null, null);
                if (NullHelper.IsNull(hudInstance))
                {
                    _elementsSearched = false; // Retry next frame
                    return;
                }

                // Get the FocusLabel (UILabel component)
                _focusLabelInstance = focusLabelField.GetValue(hudInstance);
                if (NullHelper.IsNull(_focusLabelInstance))
                {
                    _elementsSearched = false;
                    return;
                }

                // UILabel is a Component, so we can cast and get its transform
                var component = _focusLabelInstance as Component;
                if (NullHelper.IsNull(component))
                    return;

                _focusLabelTransform = component.transform;
                _originalLocalPosition = _focusLabelTransform.localPosition;

                _initialized = true;
            }
            catch
            {
                _elementsSearched = false; // Retry on error
            }
        }

    }
}
