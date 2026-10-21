// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Panels/PopupPlace.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: the horizontal rule moved to Core KitPlacement.PopupX;
// the gap is a parameter (PackPanel: PanelDress.Gap) and the container background comes from KitSkin.
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Where a pop-up under a panel hangs (PackPanel's key ring and tacklebox): centred under its button, but not left of
    /// the panel's left edge, and right of an open chest's panel when that reaches under it. Positions are the pop-up's
    /// left and top edges, from the player panel's top-right corner.
    /// </summary>
    public static class PopupPlace
    {
        private static readonly Vector3[] corners = new Vector3[4];
        private static RectTransform backgroundOf;
        private static Image background;

        public static void Under(InventoryGui gui, RectTransform popup, float slotLeft, float centre, float top, float gap)
        {
            bool container = gui.IsContainerOpen() && gui.m_container.gameObject.activeInHierarchy;
            float x = KitPlacement.PopupX(slotLeft, centre, popup.sizeDelta.x, container, container ? RightEdge(gui, gui.m_container) : 0f, gap);
            Vector2 at = new Vector2(x, top);
            if ((popup.anchoredPosition - at).sqrMagnitude > 0.01f)
                popup.anchoredPosition = at;
        }

        /// <summary>
        /// A panel's right edge, its background's reach included, from the player panel's top-right corner. The background
        /// is looked up once per panel (asked every frame a pop-up and a chest are open).
        /// </summary>
        private static float RightEdge(InventoryGui gui, RectTransform panel)
        {
            if (panel != backgroundOf)
            {
                backgroundOf = panel;
                background = KitSkin.Background(panel);
            }
            (background != null ? background.rectTransform : panel).GetWorldCorners(corners);
            return gui.m_player.InverseTransformPoint(corners[2]).x - gui.m_player.rect.xMax;
        }
    }
}
