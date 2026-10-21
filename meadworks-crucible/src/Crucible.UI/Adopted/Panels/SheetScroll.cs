// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Panels/SheetScroll.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: works without an inventory (the wheel speed is copied from
// the game's recipe list when one exists); bar width, fallback speed and colours from Core KitRowLayout/KitPalette.
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A scrolling list: a viewport clipped to its rect (<c>RectMask2D</c>) holding a content rect as tall as its rows,
    /// moved by a <c>ScrollRect</c> with the mouse wheel at the game's own recipe list's speed, and a thin bronze bar in
    /// the right margin that shows only when the list is longer than the viewport. The viewport's clear image takes the
    /// pointer, so the wheel works anywhere over the list.
    /// </summary>
    public static class SheetScroll
    {
        private const float BarWidth = KitRowLayout.ScrollBarWidth;
        private const float FallbackSensitivity = KitRowLayout.FallbackScrollSensitivity;
        private static readonly Color Track = KitUnity.Colour(KitPalette.ScrollTrack);
        private static readonly Color Handle = KitUnity.Colour(KitPalette.ScrollHandle);

        /// <summary>The list inside <paramref name="panel"/>, between <paramref name="top"/> and the margin; returns the content rect.</summary>
        public static RectTransform Make(RectTransform panel, float margin, float top)
        {
            RectTransform viewport = Child(panel, "viewport");
            Stretch(viewport, new Vector2(margin, margin), new Vector2(-margin, -top));
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Child(viewport, "content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = GameSensitivity(InventoryGui.instance);
            scroll.verticalScrollbar = Bar(panel, margin, top);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        /// <summary>The wheel's speed of the game's own recipe list, so both scroll alike.</summary>
        private static float GameSensitivity(InventoryGui gui)
        {
            ScrollRect game = gui != null && gui.m_crafting != null ? gui.m_crafting.GetComponentInChildren<ScrollRect>(true) : null;
            return game != null && game.scrollSensitivity > 0f ? game.scrollSensitivity : FallbackSensitivity;
        }

        private static Scrollbar Bar(RectTransform panel, float margin, float top)
        {
            RectTransform track = Child(panel, "scrollbar");
            track.anchorMin = new Vector2(1f, 0f);
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(-(margin + BarWidth) / 2f, margin);
            track.offsetMax = new Vector2(-(margin - BarWidth) / 2f, -top);
            track.gameObject.AddComponent<Image>().color = Track;
            RectTransform handle = Child(track, "handle");
            Stretch(handle, Vector2.zero, Vector2.zero);
            Image fill = handle.gameObject.AddComponent<Image>();
            fill.color = Handle;
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = fill;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}
