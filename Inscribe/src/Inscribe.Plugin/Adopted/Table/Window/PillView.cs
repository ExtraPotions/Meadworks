// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; TMP wrapping mode; Crucible pixels-per-unit rule.
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// One pill of a <see cref="PillStrip"/>: the rounded tag (<see cref="PillArt"/>, sliced) tinted in the essence's
    /// colour, its name in a copy of the description's font, and a <see cref="TipHover"/> for its hover box.
    /// </summary>
    internal sealed class PillView
    {
        private readonly Image _back;
        private readonly TMP_Text _label;
        private readonly TipHover _tip;

        public PillView(RectTransform parent, TMP_Text font, float fontSize)
        {
            Root = new GameObject("pill", typeof(RectTransform), typeof(Image));
            Root.layer = parent.gameObject.layer;
            Root.transform.SetParent(parent, false);
            _back = Root.GetComponent<Image>();
            _back.sprite = PillArt.Sprite;
            _back.type = Image.Type.Sliced;
            _back.raycastTarget = true;
            _tip = Root.AddComponent<TipHover>();
            _label = Object.Instantiate(font, Root.transform, false);
            _label.name = "label";
            var rect = _label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _label.enableAutoSizing = false;
            _label.fontSize = fontSize;
            _label.alignment = TextAlignmentOptions.Center;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.raycastTarget = false;
        }

        public GameObject Root { get; }

        // A sliced border's width in UI units is its pixels / (sprite pixels per unit / canvas reference pixels per unit
        // * multiplier). This multiplier makes the round ends exactly half the pill's height wide, so its ends are half
        // circles (without it the borders came out hundreds of units wide and were squeezed into one ellipse: "these
        // look like ovals", 2026-10-07).
        private static float EndScale(Image image)
        {
            return KitAppearanceRules.Multiplier(PillArt.TextureHeight / PillStrip.Height,
                image.canvas != null ? image.canvas.referencePixelsPerUnit : 0f, PillArt.PixelsPerUnit);
        }

        /// <summary>Where the pill sits in its strip, from the top left (x right, y down).</summary>
        public Vector2 At { get; private set; }

        public void Show(string name, string text, Color tone, Vector2 at, float width)
        {
            Root.SetActive(true);
            At = at;
            Rects.Place((RectTransform)Root.transform, at.x, at.y, width, PillStrip.Height);
            _back.pixelsPerUnitMultiplier = EndScale(_back);
            _back.color = tone;
            _label.text = name;
            _label.color = Color.Lerp(tone, Color.white, 0.75f);
            _tip.Set(name, text);
        }
    }
}
