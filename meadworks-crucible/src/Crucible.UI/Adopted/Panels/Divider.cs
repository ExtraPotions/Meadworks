// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Panels/Divider.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: any number of named dividers per panel; colour and
// thickness from Core KitPalette/KitRowLayout.
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>A thin bronze line across a panel (PackPanel's line over the purse row): a child image, no clicks.</summary>
    public static class Divider
    {
        public const string DefaultName = "Crucible_divider";
        private const float Thickness = KitRowLayout.DividerThickness;
        private static readonly Color Bronze = KitUnity.Colour(KitPalette.Divider);

        /// <summary>Across <paramref name="width"/> from <paramref name="left"/> (its centre line at left.y), or hidden.</summary>
        public static RectTransform Place(RectTransform panel, Vector2 left, float width, bool show, string name = DefaultName)
        {
            Transform found = panel.Find(name);
            RectTransform line = found != null ? (RectTransform)found : Make(panel, name);
            line.gameObject.SetActive(show);
            line.anchorMin = line.anchorMax = new Vector2(0f, 1f);
            line.pivot = new Vector2(0f, 0.5f);
            line.sizeDelta = new Vector2(width, Thickness);
            line.anchoredPosition = left;
            return line;
        }

        private static RectTransform Make(RectTransform panel, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform line = (RectTransform)go.transform;
            line.SetParent(panel, false);
            Image image = go.GetComponent<Image>();
            image.color = Bronze;
            image.raycastTarget = false;
            return line;
        }
    }
}
