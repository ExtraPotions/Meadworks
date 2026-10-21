using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// A box a mod added to the column: <see cref="Rect"/> is the box itself (a child of the column's container; hide it
    /// with <c>SetActive(false)</c> and the column closes the gap), <see cref="Icon"/> the image at its top showing the
    /// mod's sprite, and <see cref="Text"/> the number line along its bottom when the box keeps one.
    /// </summary>
    public sealed class Plate
    {
        public Plate(RectTransform rect, Image icon, TMP_Text? text)
        {
            Rect = rect;
            Icon = icon;
            Text = text;
        }

        public RectTransform Rect { get; }

        public Image Icon { get; }

        public TMP_Text? Text { get; }
    }
}
