using Mantle.Look;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Panels
{
    /// <summary>
    /// Where a pop-up under the slot panel hangs (the key ring's and the tacklebox's): centred under its button, but not left
    /// of the slot panel, and right of an open chest's panel when that reaches under it (the chest panel hangs under the
    /// inventory panel). Positions are the pop-up's left and top edges, from the player panel's top-right corner.
    /// </summary>
    public static class PopupPlace
    {
        private static readonly Vector3[] corners = new Vector3[4];

        public static void Under(InventoryGui gui, RectTransform popup, float slotLeft, float centre, float top)
        {
            float x = Mathf.Max(slotLeft, centre - popup.sizeDelta.x / 2f);
            if (gui.IsContainerOpen() && gui.m_container.gameObject.activeInHierarchy)
                x = Mathf.Max(x, RightEdge(gui, gui.m_container) + PanelDress.Gap);
            Vector2 at = new Vector2(x, top);
            if ((popup.anchoredPosition - at).sqrMagnitude > 0.01f)
                popup.anchoredPosition = at;
        }

        /// <summary>A panel's right edge, its background's reach included, from the player panel's top-right corner.</summary>
        private static float RightEdge(InventoryGui gui, RectTransform panel)
        {
            Image background = GridSkin.Background(panel);
            (background != null ? background.rectTransform : panel).GetWorldCorners(corners);
            return gui.m_player.InverseTransformPoint(corners[2]).x - gui.m_player.rect.xMax;
        }
    }
}
