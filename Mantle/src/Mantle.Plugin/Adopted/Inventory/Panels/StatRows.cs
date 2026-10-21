using Mantle.Core;
using ExtraPotions.Mantle.Embedded.PlateColumn;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Panels
{
    /// <summary>
    /// Draws a <see cref="StatSheet"/> as rows down a scrolling content rect (<see cref="GearStats"/>): each row a label
    /// left and a value right, the label wrapping onto more lines under itself when it does not fit beside its value
    /// (Epic Loot's sentences have no value and take the whole width); headings smaller, uppercase, bronze. Rows are
    /// reused, extra ones hidden, and the content takes the rows' height, so the list scrolls. Every text is restyled
    /// before it is written: made under a hidden panel, TextMeshPro sets a text up only when it first shows and puts its
    /// defaults back (word wrap on, its own size), which once wrapped "Attack stamina usage" out of step with its value.
    /// Each row takes the pointer (a clear image) and shows its line's breakdown in the game's tooltip, dressed in the
    /// stat boxes' bordered box (<c>Column.DressTooltip</c>); headings and Epic Loot's lines have none.
    /// </summary>
    public static class StatRows
    {
        /// <summary>The text size (the user asked for smaller words than the first 15, 2026-09-28).</summary>
        public const float Size = 12f;

        private const float HeadingSize = 10.5f;
        private const float SectionSpace = 5f;
        private const float ValueGap = 6f;
        private const float MinLabelWidth = 24f;
        private static readonly Color LabelColour = new Color(0.96f, 0.89f, 0.74f, 1f);
        private static readonly Color ValueColour = new Color(1f, 0.76f, 0.42f, 1f);
        private static readonly Color HeadingColour = new Color(0.79f, 0.64f, 0.42f, 1f);

        /// <summary>The sheet's lines top down across <paramref name="width"/>; the content takes their height.</summary>
        public static void Write(RectTransform content, StatSheet sheet, float width, TMP_FontAsset font)
        {
            float y = 0f;
            int row = 0;
            for (; row < sheet.Lines.Count; row++)
            {
                StatSheet.Line line = sheet.Lines[row];
                if (line.SpaceBefore)
                    y += SectionSpace;
                y += Lay(Row(content, row, font), line, y, width);
            }
            for (; row < content.childCount; row++)
                content.GetChild(row).gameObject.SetActive(false);
            content.sizeDelta = new Vector2(content.sizeDelta.x, y);
        }

        /// <summary>One row at <paramref name="y"/>: the value's own width at the right, the label wrapped in the rest; its height.</summary>
        private static float Lay(RectTransform row, StatSheet.Line line, float y, float width)
        {
            row.gameObject.SetActive(true);
            TMP_Text label = row.GetChild(0).GetComponent<TMP_Text>();
            TMP_Text value = row.GetChild(1).GetComponent<TMP_Text>();
            Style(label, line.Heading ? HeadingSize : Size, line.Heading ? HeadingColour : LabelColour, true);
            Style(value, Size, ValueColour, false);
            label.text = Language.Localize(line.Heading ? "<uppercase>" + line.Label + "</uppercase>" : line.Label);
            value.text = Language.Localize(line.Value);
            Tip(row, line);
            float valueWidth = line.Value.Length > 0 ? value.GetPreferredValues().x : 0f;
            float labelWidth = Mathf.Max(MinLabelWidth, width - (valueWidth > 0f ? valueWidth + ValueGap : 0f));
            float height = label.GetPreferredValues(labelWidth, 0f).y;
            Put(row, 0f, y, width, height);
            Put(label.rectTransform, 0f, 0f, labelWidth, height);
            Put(value.rectTransform, width - valueWidth, 0f, valueWidth, height);
            return height;
        }

        private static void Style(TMP_Text text, float size, Color colour, bool wraps)
        {
            text.enableAutoSizing = false;
            text.fontSize = size;
            text.color = colour;
            text.textWrappingMode = wraps ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = wraps ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight;
            text.margin = Vector4.zero;
            text.richText = true;
            text.raycastTarget = false;
        }

        private static RectTransform Row(RectTransform content, int index, TMP_FontAsset font)
        {
            if (index < content.childCount)
                return (RectTransform)content.GetChild(index);
            RectTransform row = Child(content, "row");
            Hover(row);
            Text(row, "label", font);
            Text(row, "value", font);
            return row;
        }

        /// <summary>The row takes the pointer for its tooltip; the game's own tooltip box when the bordered one is missing.</summary>
        private static void Hover(RectTransform row)
        {
            row.gameObject.AddComponent<Image>().color = Color.clear;
            KitTooltip.Attach(row.gameObject);
        }

        /// <summary>The line's breakdown under its own name; nothing for a line without one.</summary>
        private static void Tip(RectTransform row, StatSheet.Line line)
        {
            UITooltip tip = row.GetComponent<UITooltip>();
            if (tip != null)
                tip.Set(line.Tip != null ? Language.Localize(line.Label) : "", line.Tip ?? "");
        }

        private static void Text(RectTransform row, string name, TMP_FontAsset font)
        {
            GameObject go = Child(row, name).gameObject;
            go.SetActive(false);   // the text wakes with its font set, so it never looks for TextMeshPro's missing default
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            go.SetActive(true);
            text.fontSize = Size;   // set before TextMeshPro's own set-up, which then keeps ours
        }

        private static RectTransform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Top-left at (<paramref name="x"/>, <paramref name="y"/> down) in its parent, this size.</summary>
        private static void Put(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
