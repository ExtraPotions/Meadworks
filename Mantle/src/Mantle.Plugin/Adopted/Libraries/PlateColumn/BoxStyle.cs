using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// The look of one box in the column: <see cref="Column.BoxSize"/> square, its background the brown
    /// <see cref="Skin.Box"/> stretched over the whole box, the icon small at the top centre and the number (when the
    /// box has one) on a line along the bottom edge, shrinking to fit rather than wrapping. The parts are restyled in
    /// place, never replaced, so a mod holding the game's <c>m_armor</c> / <c>m_weight</c> text or a box's icon keeps a
    /// working reference, and the game goes on writing its numbers every frame. A box whose background already shows the
    /// skin's box (styled by any copy of this library) is left as it is.
    /// </summary>
    internal static class BoxStyle
    {
        private const float IconSize = 30f;
        private const float IconAloneSize = 40f;
        private const float IconTop = 6f;
        private const float TextHeight = 22f;
        private const float TextSide = 4f;
        private const float TextBottom = 4f;
        private const float FontMin = 10f;
        private const float FontMax = 16f;

        public static void Apply(RectTransform box)
        {
            Image? background = PlateParts.BackgroundOf(box);
            if (background == null || Skin.IsBox(background.sprite))
            {
                return;
            }
            TMP_Text? text = box.GetComponentInChildren<TMP_Text>(true);
            Transform? textChild = PlateParts.ChildHolding(box, text != null ? text.transform : null);
            Image? icon = PlateParts.IconOf(box, textChild);
            Frame(box);
            Background(background);
            if (icon != null)
            {
                Icon(icon);
            }
            if (text != null && textChild is RectTransform line)
            {
                Caption(text, line);
            }
        }

        /// <summary>A box with no number (OpenKeep's trash can): its icon larger and centred in the box.</summary>
        public static void IconAlone(Image icon)
        {
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(IconAloneSize, IconAloneSize);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Square and unscaled; anchored top-left as the column's layout group anchors its children, so the size reads
        /// right before the first layout pass.
        /// </summary>
        private static void Frame(RectTransform box)
        {
            box.anchorMin = new Vector2(0f, 1f);
            box.anchorMax = new Vector2(0f, 1f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(Column.BoxSize, Column.BoxSize);
            box.localScale = Vector3.one;
        }

        /// <summary>
        /// The skin's colours are baked in, so the Image is white with the default material; it takes the pointer over the
        /// whole box so the tooltip (and any button a mod puts on the box) answers anywhere on it. Drawn first.
        /// </summary>
        private static void Background(Image image)
        {
            Stretch(image.rectTransform);
            image.sprite = Skin.Box;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 1f;
            image.preserveAspect = false;
            image.color = Color.white;
            image.material = null;
            image.raycastTarget = true;
            image.rectTransform.SetSiblingIndex(0);
        }

        /// <summary>Top centre, a little under the box's top edge, drawn after the background and before the text.</summary>
        private static void Icon(Image icon)
        {
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            rect.anchoredPosition = new Vector2(0f, -IconTop);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            rect.SetSiblingIndex(1);
        }

        /// <summary>
        /// A line along the bottom edge, inset on each side, centred both ways; long numbers such as a red-flashing
        /// "1234/1500" shrink down to the minimum size instead of wrapping.
        /// </summary>
        private static void Caption(TMP_Text text, RectTransform line)
        {
            line.anchorMin = new Vector2(0f, 0f);
            line.anchorMax = new Vector2(1f, 0f);
            line.pivot = new Vector2(0.5f, 0f);
            line.sizeDelta = new Vector2(-2f * TextSide, TextHeight);
            line.anchoredPosition = new Vector2(0f, TextBottom);
            if (text.rectTransform != line)
            {
                Stretch(text.rectTransform);
            }
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = FontMin;
            text.fontSizeMax = FontMax;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.margin = Vector4.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
