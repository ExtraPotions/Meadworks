// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// One row of small icons on a creature's nameplate, on StarRow's own line: right-justified to a given edge and
    /// growing leftward, oldest leftmost, newest nearest the edge, in the room the star row leaves. When they do not all
    /// fit, the icons first shrink toward the size vanilla draws a star at, and only then do the oldest drop off the left,
    /// since the star row is never clipped to make room but these icons are allowed to be. Each icon is named with the
    /// row's prefix, so another row can find where this one starts. Used by <see cref="PouchIcons"/> (what a thief
    /// carries) and <see cref="MealIcons"/> (what a devourer has eaten).
    /// </summary>
    internal sealed class IconRow
    {
        /// <summary>Matches StarRow's own reference size, and the smallest an icon shrinks to.</summary>
        public const float VanillaSize = 10f;

        public const float Gap = 3f;
        private const float TopEdge = -3f; // the same shared line StarRow's glyphs hang from

        private readonly RectTransform _bar;
        private readonly string _prefix;
        private readonly List<GameObject> _icons = new List<GameObject>();

        public IconRow(RectTransform bar, string prefix)
        {
            _bar = bar;
            _prefix = prefix;
        }

        /// <summary>Where the shared line is free from: just past the rightmost glyph StarRow drew on this health bar,
        /// or 0 when it drew none (no stars, or coloured stars off). Read from the glyphs themselves rather than
        /// recomputed, so a star size setting can never put icons on the stars, and re-read on every poll because
        /// StarRow may build after a row's first layout.</summary>
        public static float StarRowEnd(RectTransform bar)
        {
            StarRow? row = bar != null && bar.parent != null ? bar.parent.GetComponent<StarRow>() : null;
            return row != null ? row.End() : 0f;
        }

        /// <summary>Just left of the icons another row drew under <paramref name="prefix"/>, a gap away; the bar's right
        /// edge when that row shows none. Icons already cleared (hidden, awaiting destruction) do not count.</summary>
        public static float LeftOf(RectTransform bar, string prefix)
        {
            float left = bar.rect.width;
            foreach (Transform child in bar)
            {
                if (child is RectTransform icon && child.gameObject.activeSelf
                    && child.name.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    left = Mathf.Min(left, icon.anchoredPosition.x - Gap);
                }
            }
            return left;
        }

        /// <summary>Draws the sprites, oldest first in the list, between <paramref name="left"/> and
        /// <paramref name="right"/> at <paramref name="configured"/> size, shrunk or thinned as the room demands.</summary>
        public void Layout(IList<Sprite?> sprites, float left, float right, float configured)
        {
            Clear();
            float room = right - left;
            float size = IconSize(sprites.Count, room, configured);
            int shown = Mathf.Min(sprites.Count, Fits(size, room));
            float cursor = right - (shown * size + Mathf.Max(0, shown - 1) * Gap);
            for (int i = sprites.Count - shown; i < sprites.Count; i++)
            {
                cursor += CreateIcon(sprites[i], cursor, size) + Gap;
            }
        }

        /// <summary>The configured icon size, shrunk just enough for <paramref name="count"/> icons to fit the room the
        /// star row leaves, but never below the size vanilla draws a star at: smaller than that an icon is a smudge,
        /// so past it the oldest are dropped instead.</summary>
        private static float IconSize(int count, float room, float configured)
        {
            if (count <= 0)
            {
                return configured;
            }
            float squeezed = (room - (count - 1) * Gap) / count;
            return Mathf.Min(configured, Mathf.Max(squeezed, Mathf.Min(configured, VanillaSize)));
        }

        // How many icons of this size fit in the room, gaps between them included; the epsilon keeps an exact
        // squeezed fit from rounding down by one.
        private static int Fits(float size, float room) =>
            Mathf.Max(0, Mathf.FloorToInt((room + Gap) / (size + Gap) + 0.001f));

        private float CreateIcon(Sprite? sprite, float x, float size)
        {
            GameObject icon = new GameObject(_prefix + _icons.Count, typeof(RectTransform));
            RectTransform rect = icon.GetComponent<RectTransform>();
            rect.SetParent(_bar, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 1f); // top-left pivot: hangs down from the shared top edge, like StarRow
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(x, TopEdge);
            Image image = icon.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true; // a trophy or the monster head is not always square
            image.enabled = sprite != null;
            _icons.Add(icon);
            return size;
        }

        /// <summary>Removes every icon; each is hidden at once, since destruction waits for the end of the frame.</summary>
        public void Clear()
        {
            foreach (GameObject icon in _icons)
            {
                if (icon != null)
                {
                    icon.SetActive(false);
                    Object.Destroy(icon);
                }
            }
            _icons.Clear();
        }
    }
}
