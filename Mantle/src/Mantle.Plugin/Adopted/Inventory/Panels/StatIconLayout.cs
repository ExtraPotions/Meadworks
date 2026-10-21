using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Panels
{
    /// <summary>
    /// Centers live values over icons in compact squares and restores the previous layout when disabled. HUD squares are
    /// never restored: a restore there left them at the undressed size for part of a frame, and a sibling square cloned
    /// from them then (Cairn's comfort, Keel's wind) kept that size. Applied every frame, so each value is written only
    /// when it differs: the boxes are already dressed on almost every frame.
    /// The inventory column's boxes are <see cref="ColumnHeight"/> high, Burrow's column button height, and as wide as the
    /// column (playtest 2026-10-20: the 48-unit boxes beside Burrow's 36-unit buttons left the column too tall). In the wide
    /// column (Burrow's buttons present) the icon sits at the left and the value fills the rest at a readable size; in the
    /// narrow one the value stays over the icon. The HUD squares keep <see cref="Size"/>.
    /// </summary>
    public static class StatIconLayout
    {
        public const float Size = 48f;

        /// <summary>The inventory column's box height: Burrow's tallest column button (ButtonRowStyle.ColumnMaxHeight).</summary>
        public const float ColumnHeight = 36f;

        /// <summary>The icon in an inventory column box, and the room round it.</summary>
        public const float ColumnIcon = 28f;
        public const float ColumnPad = 6f;

        /// <summary>From this width a column box puts its value beside the icon rather than over it.</summary>
        public const float WideBox = 80f;
        private static readonly Dictionary<RectTransform, Action> rects = new Dictionary<RectTransform, Action>();
        private static readonly Dictionary<TMP_Text, Action> texts = new Dictionary<TMP_Text, Action>();

        public static void Apply(RectTransform box, Image background, TMP_Text value, bool weight, bool restorable)
        {
            if (restorable) Remember(box);
            // The inventory column's boxes (restorable) take its width and Burrow's button height; the HUD copies stay square.
            float width = restorable ? StatsPanel.BoxWidth : Size;
            Resize(box, new Vector2(width, restorable ? ColumnHeight : Size));
            bool beside = restorable && value != null && width >= WideBox;
            float iconSize = restorable ? ColumnIcon : 32f;
            Vector2 iconAt = beside ? new Vector2(ColumnPad + iconSize / 2f - width / 2f, 0f) : Vector2.zero;
            if (restorable) Remember(background.rectTransform);
            RectTransform fill = background.rectTransform;
            if (fill.anchorMin != Vector2.zero) fill.anchorMin = Vector2.zero;
            if (fill.anchorMax != Vector2.one) fill.anchorMax = Vector2.one;
            if (fill.offsetMin != Vector2.zero) fill.offsetMin = Vector2.zero;
            if (fill.offsetMax != Vector2.zero) fill.offsetMax = Vector2.zero;
            for (int i = 0; i < box.childCount; i++)
            {
                Image icon = box.GetChild(i).GetComponent<Image>();
                if (icon != null && icon != background) Center(icon.rectTransform, new Vector2(iconSize, iconSize), restorable, iconAt);
            }
            if (value == null) return;
            Transform line = value.transform;
            while (line.parent != box) line = line.parent;
            Vector2 textSize = new Vector2(32f, weight ? (restorable ? ColumnHeight - 2f : 34f) : 24f);
            Vector2 textAt = Vector2.zero;
            if (beside)
            {
                // Right of the icon to the box's right edge, less the padding on each side.
                float left = ColumnPad * 2f + iconSize;
                textSize = new Vector2(width - left - ColumnPad, ColumnHeight - 2f);
                textAt = new Vector2(left + textSize.x / 2f - width / 2f, 0f);
            }
            Center((RectTransform)line, textSize, restorable, textAt);
            if (value.transform != line) Center(value.rectTransform, textSize, restorable);
            Font(value, restorable, beside ? 14f : 11f);
        }

        private static void Font(TMP_Text value, bool restorable, float largest)
        {
            if (restorable && !texts.ContainsKey(value))
            {
                float min = value.fontSizeMin, max = value.fontSizeMax;
                bool auto = value.enableAutoSizing;
                TextAlignmentOptions alignment = value.alignment;
                texts[value] = () =>
                {
                    value.fontSizeMin = min; value.fontSizeMax = max;
                    value.enableAutoSizing = auto; value.alignment = alignment;
                };
            }
            if (value.alignment != TextAlignmentOptions.Center) value.alignment = TextAlignmentOptions.Center;
            if (!value.enableAutoSizing) value.enableAutoSizing = true;
            if (value.fontSizeMin != 8f) value.fontSizeMin = 8f;
            if (value.fontSizeMax != largest) value.fontSizeMax = largest;
        }

        private static void Center(RectTransform rect, Vector2 size, bool restorable) => Center(rect, size, restorable, Vector2.zero);

        /// <summary>Anchored and pivoted at the box's middle, <paramref name="at"/> from it.</summary>
        private static void Center(RectTransform rect, Vector2 size, bool restorable, Vector2 at)
        {
            if (restorable) Remember(rect);
            Vector2 middle = new Vector2(0.5f, 0.5f);
            if (rect.anchorMin != middle) rect.anchorMin = middle;
            if (rect.anchorMax != middle) rect.anchorMax = middle;
            if (rect.pivot != middle) rect.pivot = middle;
            if (rect.anchoredPosition != at) rect.anchoredPosition = at;
            Resize(rect, size);
        }

        private static void Resize(RectTransform rect, Vector2 size)
        {
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }

        private static void Remember(RectTransform rect)
        {
            if (rects.ContainsKey(rect)) return;
            Vector2 min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot;
            Vector2 at = rect.anchoredPosition, size = rect.sizeDelta;
            rects[rect] = () =>
            {
                rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
                rect.anchoredPosition = at; rect.sizeDelta = size;
            };
        }

        public static void Restore()
        {
            foreach (var pair in rects) if (pair.Key != null) pair.Value();
            foreach (var pair in texts) if (pair.Key != null) pair.Value();
            rects.Clear();
            texts.Clear();
        }
    }
}
