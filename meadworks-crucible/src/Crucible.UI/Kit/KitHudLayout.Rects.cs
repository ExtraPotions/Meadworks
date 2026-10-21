// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitHudLayout
    {
        /// <summary>
        /// Adds the screen rectangle (pixels, y up) of every registered HUD panel that is currently shown. For native
        /// overlays that must keep clear of MeadWorks HUD panels; read when laying out, not every frame.
        /// </summary>
        public static void ShownScreenRects(List<Rect> into)
        {
            if (into == null) return;
            for (int i = 0; i < live.Count; i++)
            {
                var root = live[i].Panel.Root;
                if (root != null && root.gameObject.activeInHierarchy && ScreenRect(root, out var rect)) into.Add(rect);
            }
        }

        /// <summary>The screen rectangle of the shown registered panel with this id (any owner), e.g. "Cairn_Hud".</summary>
        public static bool ShownScreenRect(string id, out Rect screen)
        {
            screen = default;
            if (string.IsNullOrEmpty(id)) return false;
            string suffix = "/" + id;
            for (int i = 0; i < live.Count; i++)
            {
                var root = live[i].Panel.Root;
                if (live[i].Key.EndsWith(suffix, System.StringComparison.Ordinal) && root != null && root.gameObject.activeInHierarchy && ScreenRect(root, out screen)) return true;
            }
            return false;
        }

        /// <summary>The screen rectangle (pixels, y up) a rect covers, for any canvas render mode.</summary>
        public static bool ScreenRect(RectTransform rect, out Rect screen)
        {
            screen = default;
            if (rect == null) return false;
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            if (!(max.x > min.x && max.y > min.y)) return false;
            screen = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }
    }
}
