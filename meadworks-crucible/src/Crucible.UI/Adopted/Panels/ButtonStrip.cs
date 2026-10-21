// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Panels/ButtonStrip.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: the strip's name is a parameter; the default keeps
// PackPanel's interop name, which OpenKeep looks up to put its button row inside; height from Core KitButtonRow.
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A strip along the bottom of a panel kept free for a row of buttons (PackPanel keeps one for OpenKeep's Quick
    /// stack, Store all, Top up, Sort and the trash can; 26 high, 2 above the edge, 2 below the grid). It is marked by an
    /// empty object named <see cref="OpenKeepName"/> by default, a child of the panel stretched along its bottom edge,
    /// active exactly while the strip is kept: OpenKeep finds it by name and puts its row inside. No code is shared
    /// between the mods. The object has no graphic, so it takes no clicks.
    /// </summary>
    public static class ButtonStrip
    {
        public const string OpenKeepName = "PackPanel_buttonstrip";
        public const float Height = KitButtonRow.StripHeight;

        public static RectTransform Mark(RectTransform panel, bool on, string name = OpenKeepName)
        {
            RectTransform strip = panel.Find(name) as RectTransform;
            if (strip == null)
            {
                if (!on)
                    return null;
                strip = Make(panel, name);
            }
            if (strip.gameObject.activeSelf != on)
                strip.gameObject.SetActive(on);
            return strip;
        }

        private static RectTransform Make(RectTransform panel, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = panel.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, Height);
            return rect;
        }
    }
}
