using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// The HUD column's container, <c>PlateColumn_hudboxes</c>: a child of the minimap's small root
    /// (<c>Minimap.m_smallRoot</c>, the framed square at the top right, which clips nothing; only its map image does),
    /// anchored to that square's top-right corner with its pivot at its own top-left, <see cref="HudRow.Gap"/> units right
    /// of it and scaled to <see cref="HudRow.Scale"/>. A <c>VerticalLayoutGroup</c> (top aligned, children keep their
    /// size) stacks the active boxes and a <c>ContentSizeFitter</c> makes the column as tall as they are, so it grows down
    /// from the map's top edge. The game leaves only 40 units between the map and the screen's right edge; the mod laying
    /// out the HUD (Mantle) moves the map to make room. Found by name, so every copy of this library shares it; a row an
    /// older copy made (side by side under the map, a <c>HorizontalLayoutGroup</c>) is turned into the column when found.
    /// </summary>
    internal static class HudContainer
    {
        public const string Name = "PlateColumn_hudboxes";

        /// <summary>The container, made on first use; null while there is no minimap.</summary>
        public static RectTransform? Get()
        {
            GameObject? small = Minimap.instance != null ? Minimap.instance.m_smallRoot : null;
            if (small == null)
            {
                return null;
            }
            if (!(small.transform.Find(Name) is RectTransform found))
            {
                return Make(small.transform);
            }
            if (found.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                // A layout group allows no second one on the object, so the old one goes now, not at the end of the frame.
                Object.DestroyImmediate(row);
                Shape(found);
            }
            return found;
        }

        private static RectTransform Make(Transform small)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform));
            RectTransform column = (RectTransform)go.transform;
            column.SetParent(small, false);
            Shape(column);
            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.AddComponent<ColumnWatch>();
            return column;
        }

        private static void Shape(RectTransform column)
        {
            column.anchorMin = column.anchorMax = new Vector2(1f, 1f);
            column.pivot = new Vector2(0f, 1f);
            column.anchoredPosition = new Vector2(HudRow.Gap, 0f);
            column.localScale = Vector3.one * HudRow.Scale;
            VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = Column.Spacing;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }
    }
}
