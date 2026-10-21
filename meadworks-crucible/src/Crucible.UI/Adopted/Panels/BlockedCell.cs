// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Panels/BlockedCell.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: marks any cell GameObject (PackPanel: an InventoryElement);
// alpha, inset and grey from Core KitCells/KitPalette; Crucible marker name.
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A cell the grid shows but does not open: dimmed with a grey cross over it (<see cref="CrossMark"/>), and deaf to
    /// the pointer (a <c>CanvasGroup</c> that blocks no raycasts), so a click or a dragged item let go there does
    /// nothing. Gamepad navigation skips it (<see cref="KitNavigation"/>). Every cell is marked or cleared each time a
    /// grid is placed, since the same cell can be blocked under one layout and open under the next.
    /// </summary>
    public static class BlockedCell
    {
        private const string Name = "Crucible_blocked";
        private const float Dimmed = KitCells.BlockedAlpha;
        private const float Inset = KitCells.CrossInset;
        private static readonly Color Grey = KitUnity.Colour(KitPalette.BlockedCross);

        public static void Mark(GameObject cell, bool blocked)
        {
            if (cell == null)
                return;
            CanvasGroup group = cell.GetComponent<CanvasGroup>();
            if (group == null && blocked)
                group = cell.AddComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = blocked ? Dimmed : 1f;
                group.blocksRaycasts = !blocked;
                group.interactable = !blocked;
            }
            Transform cross = cell.transform.Find(Name);
            if (cross == null && blocked)
                cross = Cross(cell.transform);
            if (cross != null)
                cross.gameObject.SetActive(blocked);
        }

        public static bool IsBlocked(GameObject cell)
        {
            Transform cross = cell != null ? cell.transform.Find(Name) : null;
            return cross != null && cross.gameObject.activeSelf;
        }

        /// <summary>The grey cross over the whole cell, a little inset, drawn over everything else in it.</summary>
        private static Transform Cross(Transform cell)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(cell, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(Inset, Inset);
            rect.offsetMax = new Vector2(-Inset, -Inset);
            rect.SetAsLastSibling();
            Image image = go.GetComponent<Image>();
            image.sprite = CrossMark.Sprite;
            image.color = Grey;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }
    }
}
