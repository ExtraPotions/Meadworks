// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the equipped marker for cells that show a rarity backdrop (playtest 2026-10-20, item 13).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// The game marks a worn item with a translucent blue fill over the whole cell (<c>equiped</c>). It draws above the
    /// rarity backdrop, so a worn magic item's cell turned blue and its rarity color was lost, and a blue cell reads like
    /// the Rare color. Wherever Inscribe draws its cell look, every worn item, Normal or magic, gets this marker instead
    /// and the blue fill is off (user decision 2026-10-20): a thin rounded ring at the cell's edge, outside the backdrop's
    /// rim (58 of the icon's 64 units), in the suite's parchment label color. With Inscribe's look off (no UI, display
    /// module off, rarity stepped aside) the game's fill stays. Made once per icon as the sibling just before it (over the
    /// backdrop, under the icon); display only, on the viewing client.
    /// </summary>
    internal static class IconEquipped
    {
        private const string Name = "ecf_equipped";

        // The suite's parchment label color (Crucible SkinPalette.Label), bright against the dark cells and unlike any rarity.
        private static readonly Color Tone = new Color32(0xD8, 0xC3, 0xA0, 0xFF);

        private static readonly Dictionary<Image, Image> Markers = new Dictionary<Image, Image>();
        // Fills this hid, with the color each had: hidden by alpha, never by enabled (see Apply).
        private static readonly Dictionary<Image, Color> HiddenFills = new Dictionary<Image, Color>();
        private static readonly List<Image> Gone = new List<Image>();
        private static int _sweepAt = 64;

        /// <summary>
        /// After the game's cell update: the marker on, and the game's fill hidden, while the item is worn and Inscribe's look
        /// is drawn on the cell; otherwise the marker off, and a fill this hid gets its color back. Only fills this hid are
        /// ever touched. <paramref name="fillShowsWorn"/> is kept for the callers; the fill's enabled (grid) and GameObject
        /// (hotbar) stay the game's in both cases.
        /// </summary>
        public static void Apply(Image icon, Image? fill, bool equipped, bool fillShowsWorn)
        {
            bool marker = equipped && IconBackdrop.DrawsLook(icon);
            if (marker) Show(icon);
            else Hide(icon);
            if (fill == null) return;
            // The fill is hidden by its alpha. The game writes the grid fill's enabled every frame (InventoryGrid.UpdateGui),
            // so turning it off here switched it on and off each frame and rebuilt the inventory's canvas every frame
            // (playtest 2026-10-20, item 25). Its enabled stays the game's, and the hotbar's GameObject switch is untouched.
            if (marker)
            {
                if (!HiddenFills.ContainsKey(fill)) HiddenFills[fill] = fill.color;
                if (fill.color.a != 0f) fill.color = new Color(fill.color.r, fill.color.g, fill.color.b, 0f);
            }
            else if (HiddenFills.TryGetValue(fill, out Color was))
            {
                HiddenFills.Remove(fill);
                fill.color = was;
            }
        }

        public static void Hide(Image? icon)
        {
            if (icon != null && Markers.TryGetValue(icon, out Image marker) && marker != null && marker.enabled) marker.enabled = false;
        }

        internal static void HideAll()
        {
            foreach (Image marker in Markers.Values)
                if (marker != null) marker.enabled = false;
            RestoreFills();
        }

        internal static void Shutdown()
        {
            foreach (Image marker in Markers.Values)
                if (marker != null) KitUnity.Retire(marker.gameObject);
            Markers.Clear();
            RestoreFills();
            Gone.Clear();
            _sweepAt = 64;
        }

        /// <summary>Gives every fill this hid its color back; whether it shows stays the game's choice.</summary>
        private static void RestoreFills()
        {
            foreach (KeyValuePair<Image, Color> hidden in HiddenFills)
                if (hidden.Key != null) hidden.Key.color = hidden.Value;
            HiddenFills.Clear();
        }

        private static void Show(Image icon)
        {
            Image marker = Get(icon);
            RectTransform rect = (RectTransform)marker.transform;
            RectTransform iconRect = icon.rectTransform;
            if (rect.anchorMin != iconRect.anchorMin || rect.anchorMax != iconRect.anchorMax || rect.pivot != iconRect.pivot)
            {
                rect.anchorMin = iconRect.anchorMin;
                rect.anchorMax = iconRect.anchorMax;
                rect.pivot = iconRect.pivot;
            }
            if (rect.offsetMin != iconRect.offsetMin || rect.offsetMax != iconRect.offsetMax)
            {
                rect.offsetMin = iconRect.offsetMin;
                rect.offsetMax = iconRect.offsetMax;
            }
            if (rect.localScale != iconRect.localScale) rect.localScale = iconRect.localScale;
            if (!marker.enabled) marker.enabled = true;
        }

        private static Image Get(Image icon)
        {
            if (Markers.TryGetValue(icon, out Image marker) && marker != null) return marker;
            if (Markers.Count >= _sweepAt)
            {
                Gone.Clear();
                foreach (KeyValuePair<Image, Image> pair in Markers)
                    if (pair.Key == null || pair.Value == null) Gone.Add(pair.Key!);
                Gone.ForEach(gone => Markers.Remove(gone));
                _sweepAt = System.Math.Max(64, Markers.Count * 2);
            }
            GameObject go = new GameObject(Name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.layer = icon.gameObject.layer;
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(icon.transform.parent, false);
            rect.SetSiblingIndex(icon.transform.GetSiblingIndex());
            marker = go.GetComponent<Image>();
            marker.sprite = EquippedArt.Sprite;
            marker.type = Image.Type.Simple;
            marker.color = Tone;
            marker.raycastTarget = false;
            marker.enabled = false;
            Markers[icon] = marker;
            return marker;
        }
    }

    /// <summary>
    /// The equipped ring, painted once in code: a rounded square outline across the icon's full 64 units, 2 units wide,
    /// its inside edge clear of the rarity backdrop's 58-unit rim. White, so the Image's color is the marker's color.
    /// </summary>
    internal static class EquippedArt
    {
        private const float Span = 64f;
        private const float Corner = 6f;
        private const float Width = 2f;
        private const int Size = 128;
        private const int Samples = 4;
        private const float PixelsPerUnit = Size / Span;
        private static Sprite? _sprite;

        public static Sprite Sprite => _sprite != null ? _sprite : _sprite = Paint();

        private static Sprite Paint()
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "ecf_equipped",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            Color32[] pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < Samples; sy++)
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float ux = (x + (sx + 0.5f) / Samples) / PixelsPerUnit - Span / 2f;
                            float uy = (y + (sy + 0.5f) / Samples) / PixelsPerUnit - Span / 2f;
                            float edge = Distance(ux, uy);
                            if (edge <= 0f && edge > -Width) covered++;
                        }
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * covered / (Samples * Samples)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            made.name = "ecf_equipped";
            made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return made;
        }

        /// <summary>Signed distance from (x, y) to the rounded square's outline in units, negative inside.</summary>
        private static float Distance(float x, float y)
        {
            float qx = Mathf.Abs(x) - (Span / 2f - Corner);
            float qy = Mathf.Abs(y) - (Span / 2f - Corner);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - Corner;
        }
    }
}
