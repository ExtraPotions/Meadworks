// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>UI rectangles measured and placed from their parent's top-left corner (x right, y down).</summary>
    internal static class Rects
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>Where a child sits in its parent, from the parent's top-left corner, whatever its anchors.</summary>
        public static Rect FromTopLeft(RectTransform child)
        {
            var parent = (RectTransform)child.parent;
            child.GetWorldCorners(Corners);
            Vector2 min = parent.InverseTransformPoint(Corners[0]);
            Vector2 max = parent.InverseTransformPoint(Corners[2]);
            Rect area = parent.rect;
            return new Rect(min.x - area.xMin, area.yMax - max.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>Pins a child to its parent's top-left corner at x, y (y down), sized w by h.</summary>
        public static void Place(RectTransform child, float x, float y, float w, float h)
        {
            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0f, 1f);
            child.anchoredPosition = new Vector2(x, -y);
            child.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Pins a child where it already is, so later placements can move it by the same rules.</summary>
        public static Rect Pin(RectTransform child)
        {
            Rect at = FromTopLeft(child);
            Place(child, at.x, at.y, at.width, at.height);
            return at;
        }
    }
}
