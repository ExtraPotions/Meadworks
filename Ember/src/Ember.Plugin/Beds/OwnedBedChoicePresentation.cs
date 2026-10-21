// OpenKeep. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Adapted from Homestead BedChoiceLabel/BedPinLook/BedPing/BedBubble at 684671f98de54db6ea6652092ec041f44016c37d.
// Changed by expDARE, 2026: local owned presentation, wrapped localization and conditional foreign-UI preservation.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Ember.Beds;
using ExtraPotions.Ember.Localization;
using TMPro;
using UnityEngine;

namespace ExtraPotions.Ember.Runtime
{
    internal static class OwnedBedChoicePresentation
    {
        sealed class PinState
        {
            internal Minimap.PinData Pin;
            internal bool Double, Animate;
            internal RectTransform? Rect;
            internal Vector2 Size;
            internal Canvas? ForeignCanvas;
        }
        static readonly Dictionary<Minimap.PinData, PinState> changed = new Dictionary<Minimap.PinData, PinState>();
        static readonly List<Canvas> canvases = new List<Canvas>();
        static readonly List<RectTransform> bubbles = new List<RectTransform>();
        static readonly Vector3[] corners = new Vector3[4];
        static Minimap.PinData? ping;
        static TMP_Text? label;
        static RectTransform? measured;
        static string lastLabel = "";
        internal static float FontSize => label ? label.fontSize : 0f;
        internal static float ScreenFontSize => label ? label.fontSize * Mathf.Abs(label.transform.lossyScale.y) : 0f;

        internal static void Open(Minimap map, BedPoint nearest)
        {
            ping = new Minimap.PinData { m_type = Minimap.PinType.Ping, m_name = "", m_pos = new Vector3(nearest.X, nearest.Y, nearest.Z),
                m_icon = map.GetSprite(Minimap.PinType.Ping), m_save = false, m_doubleSize = true, m_animate = true };
            map.m_pins.Add(ping); map.m_pinUpdateRequired = true;
            Label(map, OwnedBedChoice.SecondsLeft);
        }

        static void Enlarge(Minimap.PinData? pin)
        {
            if (pin == null) return;
            if (!changed.ContainsKey(pin))
            {
                var rect = pin.m_uiElement;
                changed.Add(pin, new PinState { Pin = pin, Double = pin.m_doubleSize, Animate = pin.m_animate,
                    Rect = rect, Size = rect ? rect.sizeDelta : Vector2.zero, ForeignCanvas = rect ? rect.GetComponent<Canvas>() : null });
            }
            pin.m_doubleSize = true; pin.m_animate = true;
        }
        internal static void BeforePins(Minimap map)
        {
            foreach (var pin in OwnedBedPins.Drawn) Enlarge(pin);
            Enlarge(map.m_spawnPointPin);
        }

        static void AboveMap(Minimap.PinData? pin, Minimap map)
        {
            var icon = pin?.m_uiElement;
            if (!icon) return;
            var own = icon.GetComponent<Canvas>();
            if (!own)
            {
                var parent = icon.GetComponentInParent<Canvas>();
                own = icon.gameObject.AddComponent<Canvas>(); own.overrideSorting = true;
                if (parent) { own.sortingLayerID = parent.rootCanvas.sortingLayerID; own.sortingOrder = parent.rootCanvas.sortingOrder + 1; }
                canvases.Add(own);
            }
            if (icon.Find("Ember.BedChoice.ClickArea")) return;
            var go = new GameObject("Ember.BedChoice.ClickArea", typeof(RectTransform));
            go.layer = icon.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(icon, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.one * (map.m_pinSizeLarge * 2f);
            bubbles.Add(rect); measured = rect;
        }
        internal static void AfterPins(Minimap map)
        {
            foreach (var pin in OwnedBedPins.Drawn) AboveMap(pin, map);
            AboveMap(map.m_spawnPointPin, map);
            if (ping?.m_iconElement) ping.m_iconElement.color = new Color(1f, .76f, .2f, 1f);
        }

        internal static double WorldRadius(Minimap map)
        {
            if (!measured || !measured.gameObject.activeInHierarchy) return 0;
            measured.GetWorldCorners(corners);
            var centre = (corners[0] + corners[2]) * .5f;
            var edge = centre + new Vector3((corners[2].x - corners[0].x) * .5f, 0, 0);
            var a = map.ScreenToWorldPoint(centre); var b = map.ScreenToWorldPoint(edge);
            return BedPoints.MapDistance(new BedPoint(a.x, a.y, a.z), new BedPoint(b.x, b.y, b.z));
        }

        internal static void Label(Minimap map, int seconds)
        {
            var parent = map.m_mapImageLarge.transform;
            if (!label || label.transform.parent != parent)
            {
                if (label) Object.Destroy(label.gameObject);
                var go = new GameObject("Ember.BedChoice.Label", typeof(RectTransform));
                var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.SetAsLastSibling();
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
                rect.anchoredPosition = new Vector2(18f, -18f);
                rect.sizeDelta = new Vector2(Mathf.Max(200f, Mathf.Min(560f, map.m_mapImageLarge.rectTransform.rect.width - 36f)), 220f);
                go.SetActive(false);
                label = go.AddComponent<TextMeshProUGUI>();
                label.font = map.m_biomeNameLarge.font;
                label.fontSharedMaterial = map.m_biomeNameLarge.fontSharedMaterial;
                label.color = map.m_biomeNameLarge.color; label.fontSize = 24f; label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.TopLeft; label.textWrappingMode = TextWrappingModes.Normal;
                label.raycastTarget = false; go.SetActive(true); lastLabel = "";
            }
            var text = "<size=180%>" + EmberText.Format(EmberTextKeys.BedChoiceCountdown, seconds) + "</size>\n"
                + EmberText.Text(EmberTextKeys.BedChoiceNearest) + "\n" + EmberText.Text(EmberTextKeys.BedChoiceClick) + "\n" + EmberText.Text(EmberTextKeys.BedChoiceKeys);
            if (text != lastLabel) { label.text = text; lastLabel = text; }
        }

        internal static void Close(Minimap? map)
        {
            if (label) Object.Destroy(label.gameObject);
            label = null; lastLabel = "";
            if (map && ping != null && map.m_pins.Contains(ping)) map.RemovePin(ping);
            ping = null;
            foreach (var state in changed.Values)
            {
                bool ownedSize = state.Pin.m_doubleSize;
                if (ownedSize) state.Pin.m_doubleSize = state.Double;
                if (state.Pin.m_animate) state.Pin.m_animate = state.Animate;
                var current = state.Pin.m_uiElement;
                bool foreignCanvas = false;
                if (current) foreach (var canvas in current.GetComponents<Canvas>())
                    if (canvas && !canvases.Contains(canvas)) foreignCanvas = true;
                if (foreignCanvas)
                {
                    if (ownedSize && state.Rect && ReferenceEquals(state.Rect, current)) state.Rect.sizeDelta = state.Size;
                }
                else if (map) map.DestroyPinMarker(state.Pin);
            }
            changed.Clear();
            foreach (var canvas in canvases) if (canvas) Object.Destroy(canvas);
            foreach (var bubble in bubbles) if (bubble) Object.Destroy(bubble.gameObject);
            canvases.Clear(); bubbles.Clear(); measured = null;
        }
    }
}
