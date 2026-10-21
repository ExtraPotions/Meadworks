using UnityEngine;
using UnityEngine.UI;

namespace Burrow.Stow
{
    /// <summary>The three marks on one grid element: a star (favourite item) in the top left corner, a cross (junk)
    /// in the top right corner and a border (favourite slot). Added to the element when first seen.</summary>
    public sealed class SlotMarks : MonoBehaviour
    {
        private const float MarkSize = 14f;
        private static readonly Color StarColour = new Color(1f, 0.85f, 0.25f, 0.95f);
        private static readonly Color CrossColour = new Color(1f, 0.35f, 0.3f, 0.95f);
        private static readonly Color BorderColour = new Color(0.45f, 0.75f, 1f, 0.9f);

        private Image star;
        private Image cross;
        private Image border;

        public static SlotMarks Attach(InventoryElement element)
        {
            SlotMarks marks = element.gameObject.AddComponent<SlotMarks>();
            marks.border = marks.Add("Burrow_border", StowSprites.Border, BorderColour, Vector2.zero, Vector2.one, Vector2.zero);
            marks.border.type = Image.Type.Sliced;
            marks.star = marks.Add("Burrow_star", StowSprites.Star, StarColour, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(MarkSize, MarkSize));
            marks.cross = marks.Add("Burrow_cross", StowSprites.Cross, CrossColour, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(MarkSize, MarkSize));
            marks.Set(false, false, false);
            return marks;
        }

        public void Set(bool favouriteItem, bool favouriteSlot, bool junk)
        {
            star.enabled = favouriteItem;
            border.enabled = favouriteSlot;
            cross.enabled = junk;
        }

        private Image Add(string name, Sprite sprite, Color colour, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = new Vector2(anchorMin.x > 0.5f ? -2f : 2f, -2f) * (size == Vector2.zero ? 0f : 1f);
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }
    }
}
