// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public sealed partial class KitMapView
    {
        static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Bottom-left anchored and pivoted, so anchored positions are <see cref="KitMapCamera"/> view pixels.</summary>
        static RectTransform Corner(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            return rect;
        }

        static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.zero;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>A code-drawn square outline (four bars) centered on its anchored position.</summary>
        static RectTransform Outline(RectTransform parent, string name, float size, Color colour, float thickness)
        {
            var root = Corner(Child(parent, name));
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(size, size);
            Bar(root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -thickness), Vector2.zero, colour);
            Bar(root, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, thickness), colour);
            Bar(root, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(thickness, 0), colour);
            Bar(root, new Vector2(1, 0), Vector2.one, new Vector2(-thickness, 0), Vector2.zero, colour);
            root.gameObject.SetActive(false);
            return root;
        }

        static void Bar(RectTransform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, Color colour)
        {
            var bar = Child(parent, "Bar");
            bar.anchorMin = min; bar.anchorMax = max; bar.offsetMin = offsetMin; bar.offsetMax = offsetMax;
            var image = bar.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
        }

        (RectTransform root, Image icon, TMP_Text label) NewPin(int index)
        {
            var root = Corner(Child(pinLayer, "Pin" + index));
            root.sizeDelta = Vector2.zero;
            var iconRect = Child(root, "Icon");
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var label = KitCanvas.Text(root, "Label", "", KitMapLayout.LabelSize, KitUnity.Colour(KitMapLayout.Label), TextAlignmentOptions.Top, false);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.sizeDelta = new Vector2(160, KitMapLayout.LabelSize + 4);
            return (root, icon, label);
        }

        /// <summary>A screen point in view pixels from the view's bottom-left corner.</summary>
        internal bool ToView(Vector2 screen, Camera eventCamera, out KitVector view)
        {
            view = KitVector.Zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, eventCamera, out var local)) return false;
            var rect = Root.rect;
            view = new KitVector(local.x - rect.xMin, local.y - rect.yMin);
            return true;
        }
    }

    /// <summary>Mouse input of a <see cref="KitMapView"/>: drag pans, wheel zooms about the pointer, click selects a pin.</summary>
    public sealed class KitMapInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler, IPointerClickHandler
    {
        internal KitMapView View;

        public void OnBeginDrag(PointerEventData data) => View?.HidePadCursor();

        public void OnDrag(PointerEventData data)
        {
            if (View == null) return;
            if (View.ToView(data.position, data.pressEventCamera, out var now) && View.ToView(data.position - data.delta, data.pressEventCamera, out var before))
            {
                View.Camera.Pan(now - before);
                View.Refresh();
            }
        }

        public void OnScroll(PointerEventData data)
        {
            if (View == null) return;
            int direction = KitClicks.Wheel(data.scrollDelta.y);
            if (direction == 0 || !View.ToView(data.position, data.enterEventCamera, out var at)) return;
            View.HidePadCursor();
            View.Camera.ZoomBy(direction, at);
            View.Refresh();
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (View == null || data.button != PointerEventData.InputButton.Left || data.dragging) return;
            View.HidePadCursor();
            if (View.ToView(data.position, data.pressEventCamera, out var at)) View.Pick(at, KitMapLayout.HitRadius);
        }

        void OnRectTransformDimensionsChange() => View?.Refresh();
    }
}
