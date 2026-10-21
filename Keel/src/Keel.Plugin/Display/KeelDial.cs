// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The game's steering indicator sits on Crucible's iron-rimmed wooden dial,
// with N/E/S/W on the wood between the indicator and the ring, turned with the camera so N points north.
using ExtraPotions.Keel.Integration;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Keel.Runtime
{
    internal static class KeelDial
    {
        // Wood band for the letters between the steering circle and the ring, in UI units.
        private const float LetterBand = 30f;
        private static readonly string[] Points = { "N", "E", "S", "W" };
        private static readonly Color LetterColour = new Color(0.93f, 0.86f, 0.70f, 1f);
        private static Image? dial;
        private static RectTransform? rose;
        private static RectTransform? wind;
        private static float heading = float.NaN;

        /// <summary>The dial while it shows (the ship panel sits below it), else null.</summary>
        public static RectTransform? Shown => dial != null && dial.gameObject.activeInHierarchy ? dial.rectTransform : null;

        public static void Update()
        {
            var hud = Hud.instance;
            var root = hud != null ? hud.m_shipWindIndicatorRoot : null;
            if (root == null || !KitForged.On || !KeelModules.ProductActive(out _))
            {
                Hide();
                return;
            }
            if (dial == null || wind != root)
            {
                Dispose();
                if (!Create(root)) return;
            }
            if (!dial!.gameObject.activeSelf) dial.gameObject.SetActive(true);
            var camera = GameCamera.instance;
            if (camera == null) return;
            var forward = camera.transform.forward;
            var now = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            if (!float.IsNaN(heading) && Mathf.Abs(Mathf.DeltaAngle(now, heading)) < 0.5f) return;
            heading = now;
            // Facing east, north lies to the left: the rose turns counter-clockwise by the heading; letters stay upright.
            rose!.localRotation = Quaternion.Euler(0f, 0f, now);
            foreach (RectTransform letter in rose) letter.localRotation = Quaternion.Euler(0f, 0f, -now);
        }

        private static bool Create(RectTransform root)
        {
            if (!(root.parent is RectTransform parent)) return false;
            wind = root;
            var go = new GameObject("Keel_DialFrame", typeof(RectTransform), typeof(Image));
            go.layer = root.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.SetSiblingIndex(root.GetSiblingIndex());
            rect.anchorMin = root.anchorMin;
            rect.anchorMax = root.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            var circle = Mathf.Max(root.rect.width, root.rect.height);
            var outer = (circle + 2f * LetterBand) / (1f - 2f * KitForged.DialRing);
            rect.sizeDelta = new Vector2(outer, outer);
            // Centered on the steering circle whatever its own pivot.
            var centre = root.rect.center;
            rect.localPosition = root.localPosition + root.localRotation * new Vector3(centre.x, centre.y, 0f);
            dial = go.GetComponent<Image>();
            dial.raycastTarget = false;
            if (!KitForged.DialFrame(dial))
            {
                Dispose();
                return false;
            }
            rose = new GameObject("Rose", typeof(RectTransform)).GetComponent<RectTransform>();
            rose.gameObject.layer = go.layer;
            rose.SetParent(rect, false);
            rose.anchorMin = rose.anchorMax = rose.pivot = new Vector2(0.5f, 0.5f);
            rose.sizeDelta = Vector2.zero;
            var radius = circle / 2f + LetterBand / 2f;
            for (var i = 0; i < Points.Length; i++)
            {
                var text = KitCanvas.Text(rose, Points[i], Points[i], 16, LetterColour, TextAlignmentOptions.Center, false);
                var at = text.rectTransform;
                at.anchorMin = at.anchorMax = at.pivot = new Vector2(0.5f, 0.5f);
                at.sizeDelta = new Vector2(LetterBand, LetterBand);
                var angle = -i * 90f * Mathf.Deg2Rad;
                at.anchoredPosition = new Vector2(-Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
                text.fontStyle = FontStyles.Bold;
                text.raycastTarget = false;
            }
            heading = float.NaN;
            return true;
        }

        private static void Hide()
        {
            if (dial != null && dial.gameObject.activeSelf) dial.gameObject.SetActive(false);
            heading = float.NaN;
        }

        public static void Dispose()
        {
            if (dial != null) Object.Destroy(dial.gameObject);
            dial = null;
            rose = null;
            wind = null;
            heading = float.NaN;
        }
    }
}
