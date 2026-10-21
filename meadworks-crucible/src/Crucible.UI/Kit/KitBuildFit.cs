// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The build menu's window is sized to its rows, so the cultivator's two rows
// no longer sit in a hammer-sized empty board; a tool with more rows gets the remembered full height back.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    internal static class KitBuildFit
    {
        private static readonly AccessTools.FieldRef<BuildUi, RectTransform> ViewRef = AccessTools.FieldRefAccess<BuildUi, RectTransform>("m_pieceView");
        private static readonly AccessTools.FieldRef<BuildUi, RectTransform> ContainerRef = AccessTools.FieldRefAccess<BuildUi, RectTransform>("m_pieceButtonsContainer");
        // The game's own bottom edges (offsetMin.y), read before the first change.
        private static readonly Dictionary<RectTransform, float> bottoms = new Dictionary<RectTransform, float>();
        private static readonly Vector3[] corners = new Vector3[4];

        /// <summary>After the menu fills its buttons: shrink the piece view and the boards around it to the rows shown, top edges kept.</summary>
        internal static void Fit(BuildUi ui)
        {
            RectTransform view = ViewRef(ui), container = ContainerRef(ui);
            GridLayoutGroup grid = container != null ? container.GetComponent<GridLayoutGroup>() : null;
            if (view == null || grid == null)
                return;
            foreach (RectTransform raised in new List<RectTransform>(bottoms.Keys))
                if (raised == null) bottoms.Remove(raised); else Restore(raised);
            if (!KitLook.On)
                return;
            int columns = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? grid.constraintCount
                : KitBuildTabRules.FitColumns(container.rect.width - grid.padding.horizontal, grid.cellSize.x, grid.spacing.x);
            int pieces = 0;
            foreach (Transform child in container)
                if (child.gameObject.activeSelf) pieces++;
            float shrink = KitBuildTabRules.FitShrink(pieces, columns, grid.cellSize.y, grid.spacing.y, grid.padding.vertical, view.rect.height);
            if (shrink <= 0f)
                return;
            // Every box of the menu that encloses the view and reaches below it (the window boards, the view itself),
            // outermost first; each ends up exactly `shrink` shorter, whether it stretches with its parent or not.
            view.GetWorldCorners(corners);
            float viewBottom = corners[0].y, viewTop = corners[1].y, middle = (corners[0].x + corners[2].x) / 2f, slack = 1f * view.lossyScale.y;
            var boxes = new List<(RectTransform Rect, int Depth, float Height, float Shrink)>();
            foreach (RectTransform rect in ui.GetComponentsInChildren<RectTransform>(false))
            {
                // The view and the drawn boards only: a bare full-screen holder keeps its size (the hint box hangs from its bottom).
                if (rect != view && (rect.IsChildOf(view) || rect.GetComponent<Image>() == null))
                    continue;
                rect.GetWorldCorners(corners);
                if (corners[0].x > middle || corners[2].x < middle || corners[1].y < viewTop - slack || corners[0].y > viewBottom + slack)
                    continue;
                int depth = 0;
                for (Transform at = rect; at != null && at != ui.transform; at = at.parent) depth++;
                float local = rect.lossyScale.y != 0f ? shrink * view.lossyScale.y / rect.lossyScale.y : shrink;
                boxes.Add((rect, depth, rect.rect.height, local));
            }
            boxes.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            foreach (var box in boxes)
            {
                float target = box.Height - box.Shrink;
                if (box.Rect.rect.height > target + 0.5f)
                    Raise(box.Rect, box.Rect.rect.height - target);
            }
        }

        private static void Restore(RectTransform rect)
        {
            if (rect != null && bottoms.TryGetValue(rect, out float bottom))
                rect.offsetMin = new Vector2(rect.offsetMin.x, bottom);
        }

        private static void Raise(RectTransform rect, float by)
        {
            if (rect == null)
                return;
            if (!bottoms.ContainsKey(rect))
                bottoms[rect] = rect.offsetMin.y;
            rect.offsetMin = new Vector2(rect.offsetMin.x, bottoms[rect] + by);
        }

    }
}
