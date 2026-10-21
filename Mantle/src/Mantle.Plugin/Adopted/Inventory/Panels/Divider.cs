using UnityEngine;
using UnityEngine.UI;

namespace Mantle.Panels
{
    /// <summary>The thin bronze line over the slot panel's purse row (the user's mockup): a child image, no clicks.</summary>
    public static class Divider
    {
        private const string Name = "Mantle_divider";
        private const float Thickness = 2f;
        private static readonly Color Bronze = new Color(0.55f, 0.4f, 0.24f, 0.85f);

        /// <summary>Across <paramref name="width"/> from <paramref name="left"/> (its centre line at left.y), or hidden.</summary>
        public static void Place(RectTransform panel, Vector2 left, float width, bool show)
        {
            Transform found = panel.Find(Name);
            RectTransform line = found != null ? (RectTransform)found : Make(panel);
            line.gameObject.SetActive(show);
            line.anchorMin = line.anchorMax = new Vector2(0f, 1f);
            line.pivot = new Vector2(0f, 0.5f);
            line.sizeDelta = new Vector2(width, Thickness);
            line.anchoredPosition = left;
        }

        private static RectTransform Make(RectTransform panel)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform), typeof(Image));
            RectTransform line = (RectTransform)go.transform;
            line.SetParent(panel, false);
            Image image = go.GetComponent<Image>();
            image.color = Bronze;
            image.raycastTarget = false;
            return line;
        }
    }
}
