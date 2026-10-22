// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Barter;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>
    /// Pins the purse and the sell list from the store's screen box. Both are placed on every call, so a resolution
    /// change while the store is open moves them again.
    /// </summary>
    internal static class PanelPlacement
    {
        const float Gap = 6f;
        static readonly Vector3[] corners = new Vector3[4];

        internal static void Apply(RectTransform panel)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy || !(panel.parent is RectTransform store))
                return;
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas == null)
                return;
            RectTransform root = canvas.rootCanvas.transform as RectTransform;
            if (root == null)
                return;
            Camera camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            if (!TryBox(root, camera, out PanelBox canvasBox) || !TryBox(store, camera, out PanelBox storeBox))
                return;

            var sell = store.Find(SellPanel.ObjectName) as RectTransform;
            var purse = store.Find(PurseCard.ObjectName) as RectTransform;
            bool sellOn = TrySize(sell, camera, out float sellWidth, out float sellHeight, out float sellUnit);
            bool purseOn = TrySize(purse, camera, out float purseWidth, out float purseHeight, out float purseUnit);
            float unit = 1f;
            if (sellOn) unit = Mathf.Max(unit, sellUnit);
            if (purseOn) unit = Mathf.Max(unit, purseUnit);
            OpenPlace place = SidePanelFit.PlaceBoth(
                Visible(canvasBox), storeBox,
                sellOn, sellWidth, sellHeight,
                purseOn, purseWidth, purseHeight,
                Gap * unit, KitSkin.PanelOverhang * unit);
            if (sellOn) Move(sell, store, camera, place.Sell.Box);
            if (purseOn) Move(purse, store, camera, place.Purse.Box);
        }

        static bool TrySize(RectTransform rect, Camera camera, out float width, out float height, out float unit)
        {
            width = height = unit = 0f;
            if (rect == null || !rect.gameObject.activeInHierarchy)
                return false;
            Vector2 scale = PixelsPerUnit(rect, camera);
            unit = Mathf.Max(scale.x, scale.y);
            width = (rect.rect.width > 1f ? rect.rect.width : rect.sizeDelta.x) * scale.x;
            height = (rect.rect.height > 1f ? rect.rect.height : rect.sizeDelta.y) * scale.y;
            return width > 1f && height > 1f;
        }

        static PanelBox Visible(PanelBox canvas)
        {
            float screenW = Screen.width;
            float screenH = Screen.height;
            if (screenW < 1f || screenH < 1f)
                return canvas;
            float x = Mathf.Max(canvas.X, 0f);
            float y = Mathf.Max(canvas.Y, 0f);
            float right = Mathf.Min(canvas.Right, screenW);
            float top = Mathf.Min(canvas.Top, screenH);
            if (right - x < 1f || top - y < 1f)
                return canvas;
            return new PanelBox(x, y, right - x, top - y);
        }

        static bool TryBox(RectTransform rect, Camera camera, out PanelBox box)
        {
            box = default;
            if (rect == null)
                return false;
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                if (!PanelBox.Finite(point.x) || !PanelBox.Finite(point.y))
                    return false;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            if (max.x - min.x < 1f || max.y - min.y < 1f)
                return false;
            box = new PanelBox(min.x, min.y, max.x - min.x, max.y - min.y);
            return true;
        }

        static Vector2 PixelsPerUnit(RectTransform rect, Camera camera)
        {
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.zero));
            Vector2 right = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.right));
            Vector2 up = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.up));
            float x = Vector2.Distance(origin, right);
            float y = Vector2.Distance(origin, up);
            return new Vector2(x > 0.05f ? x : 1f, y > 0.05f ? y : 1f);
        }

        static void Move(RectTransform panel, RectTransform parent, Camera camera, PanelBox place)
        {
            var screen = new Vector2(place.X + place.Width * panel.pivot.x, place.Y + place.Height * panel.pivot.y);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, camera, out Vector2 local))
                return;
            Rect parentRect = parent.rect;
            float ax = Mathf.Lerp(parentRect.xMin, parentRect.xMax, (panel.anchorMin.x + panel.anchorMax.x) * 0.5f);
            float ay = Mathf.Lerp(parentRect.yMin, parentRect.yMax, (panel.anchorMin.y + panel.anchorMax.y) * 0.5f);
            Vector2 anchored = local - new Vector2(ax, ay);
            if ((panel.anchoredPosition - anchored).sqrMagnitude > 0.25f)
                panel.anchoredPosition = anchored;
        }
    }
}
