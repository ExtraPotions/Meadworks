using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Burrow.Look
{
    /// <summary>
    /// Gives a copied game button the Meadworks kit look, as Crucible.UI's <c>GameButtons</c> does for every
    /// <c>KitButton</c>: the components that would reset its colors or caption are taken off (the game's button text
    /// and image colors, its localizer), the face is lit and the caption warm, auto-sized between the kit's sizes on
    /// one line with an ellipsis. Unlike a kit button, the copy keeps the game's gamepad key and Unity navigation, so
    /// gamepad selection in the inventory works as before. The shared Crucible.UI binding follows the existing suite look toggle.
    /// </summary>
    public static class ButtonLook
    {
        private static readonly Color Face = Colour(ButtonRowStyle.Face);
        private static readonly Color Caption = Colour(ButtonRowStyle.Caption);

        public static void Apply(GameObject go, string caption)
        {
            if (go == null)
                return;
            foreach (ButtonTextColor colour in go.GetComponentsInChildren<ButtonTextColor>(true))
                Object.DestroyImmediate(colour);
            foreach (ButtonImageColor colour in go.GetComponentsInChildren<ButtonImageColor>(true))
                Object.DestroyImmediate(colour);
            foreach (Localize localize in go.GetComponentsInChildren<Localize>(true))
                Object.DestroyImmediate(localize);
            Image image = go.GetComponent<Image>();
            if (image != null)
                image.color = Face;
            TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
                return;
            text.text = caption;
            text.color = Caption;
            text.enableAutoSizing = true;
            text.fontSizeMin = ButtonRowStyle.CaptionMinSize;
            text.fontSizeMax = ButtonRowStyle.CaptionMaxSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            KitColor margin = ButtonRowStyle.CaptionMargin;
            text.margin = new Vector4(margin.R, margin.G, margin.B, margin.A);
            KitForged.Button(go.GetComponent<Button>());
        }

        private static Color Colour(KitColor colour) => new Color(colour.R, colour.G, colour.B, colour.A);
    }
}
