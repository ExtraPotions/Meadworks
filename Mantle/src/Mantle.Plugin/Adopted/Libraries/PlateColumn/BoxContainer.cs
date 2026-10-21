using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// The one object that holds the column: <c>PlateColumn_boxes</c>, a direct child of the player panel pinned outside
    /// its top-right corner (<see cref="Column.Left"/> right of the edge, <see cref="Column.Top"/> under the top), as wide
    /// as a box. A <c>VerticalLayoutGroup</c> stacks its active children top down in sibling order and a
    /// <c>ContentSizeFitter</c> makes it as tall as they are, so any copy of this library only has to put the boxes in
    /// rank order: Unity places them and closes the gap a hidden one leaves. Found by name, so every copy uses the same
    /// one. It sits right after the panel's background (<c>Bkg</c>), drawn above it and below everything else on the
    /// panel (the game's boxes pinned over their seats among them), and carries a <see cref="SeatWatch"/>.
    /// </summary>
    internal static class BoxContainer
    {
        public const string Name = "PlateColumn_boxes";

        /// <summary>The player panel's background image, stretched over the panel.</summary>
        private const string PanelBackground = "Bkg";

        /// <summary>Whether <paramref name="t"/> is the container on <paramref name="panel"/>.</summary>
        public static bool Is(Transform? t, Transform panel) => t != null && t.parent == panel && t.name == Name;

        /// <summary>The panel's container, made the first time it is asked for.</summary>
        public static RectTransform Get(RectTransform panel)
        {
            RectTransform? boxes = panel.Find(Name) as RectTransform;
            if (boxes == null)
            {
                boxes = Make(panel);
            }
            PlaceAfterBackground(panel, boxes);
            if (!PlateParts.Carries(boxes.gameObject, typeof(SeatWatch)))
            {
                boxes.gameObject.AddComponent<SeatWatch>();
            }
            return boxes;
        }

        private static RectTransform Make(RectTransform panel)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform));
            go.layer = panel.gameObject.layer;
            RectTransform boxes = (RectTransform)go.transform;
            boxes.SetParent(panel, false);
            boxes.anchorMin = Vector2.one;
            boxes.anchorMax = Vector2.one;
            boxes.pivot = new Vector2(0f, 1f);
            boxes.anchoredPosition = new Vector2(Column.Left, -Column.Top);
            boxes.sizeDelta = new Vector2(Column.BoxSize, Column.BoxSize);
            Stack(go);
            return boxes;
        }

        /// <summary>Boxes keep the size they are given; the layout only places them, centred, one under the other.</summary>
        private static void Stack(GameObject go)
        {
            VerticalLayoutGroup stack = go.AddComponent<VerticalLayoutGroup>();
            stack.spacing = Column.Spacing;
            stack.childAlignment = TextAnchor.UpperCenter;
            stack.childControlWidth = false;
            stack.childControlHeight = false;
            stack.childForceExpandWidth = false;
            stack.childForceExpandHeight = false;
            ContentSizeFitter fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Right after the background; nothing moves when it is there already or the panel has no background.</summary>
        private static void PlaceAfterBackground(Transform panel, Transform boxes)
        {
            Transform background = panel.Find(PanelBackground);
            if (background == null)
            {
                return;
            }
            int at = background.GetSiblingIndex();
            int now = boxes.GetSiblingIndex();
            if (now != at + 1)
            {
                boxes.SetSiblingIndex(now < at ? at : at + 1);
            }
        }
    }
}
