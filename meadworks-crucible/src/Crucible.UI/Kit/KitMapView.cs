// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>One pin of a <see cref="KitMapView"/>: a map point (0..1, v up), a runtime game sprite, a label and a tint.</summary>
    public struct KitMapPin
    {
        public Vector2 At;
        public Sprite Icon;
        public string Label;
        public Color Tint;
        /// <summary>Degrees counter-clockwise (the player arrow).</summary>
        public float Rotation;
        /// <summary>Shown even where the explored mask says unexplored (the player, the current portal).</summary>
        public bool AlwaysShow;
        /// <summary>Icon size in view pixels; 0 uses <see cref="KitMapLayout.PinSize"/>.</summary>
        public float Size;

        public KitMapPin(Vector2 at, Sprite icon, string label, bool alwaysShow = false)
        {
            At = at; Icon = icon; Label = label; Tint = Color.white; Rotation = 0; AlwaysShow = alwaysShow; Size = 0;
        }
    }

    /// <summary>
    /// KX09 kit map view (Threshold portal picker map pane, Cairn Markers tab): a texture region in a PackPanel inset
    /// frame with uniform zoom 0.25 to 8 and clamped pan (<see cref="KitMapCamera"/>), pooled pins with kit-font labels and
    /// a selection ring, an explored mask from a supplied delegate, mouse drag/wheel/click and a pad cursor the product
    /// drives through <see cref="Pad"/>. Never created headless or before the graphical kit is active. The product owns
    /// the textures and sprites (the game's own, used at runtime) and its input focus rules.
    /// </summary>
    public sealed partial class KitMapView : IDisposable
    {
        public RectTransform Root { get; }
        public KitMapCamera Camera { get; }
        /// <summary>The selected pin index, or -1.</summary>
        public int Selected { get; private set; } = -1;
        /// <summary>Called with the pin index after a click or pad A selects a pin (not after <see cref="Select"/>).</summary>
        public Action<int> OnSelect { get; set; }
        public bool ShowLabels { get => labels; set { labels = value; Refresh(); } }
        public IReadOnlyList<KitMapPin> Pins => pins;

        readonly RawImage map;
        readonly RawImage fog;
        readonly RectTransform pinLayer;
        readonly RectTransform ring;
        readonly RectTransform cursor;
        readonly List<KitMapPin> pins = new List<KitMapPin>();
        readonly List<KitVector> points = new List<KitVector>();
        readonly List<(RectTransform root, Image icon, TMP_Text label)> pool = new List<(RectTransform, Image, TMP_Text)>();
        Func<KitVector, bool> explored;
        bool labels = true;
        bool disposed;

        KitMapView(RectTransform root, RawImage map, RawImage fog, RectTransform pinLayer, RectTransform ring, RectTransform cursor)
        {
            Root = root; this.map = map; this.fog = fog; this.pinLayer = pinLayer; this.ring = ring; this.cursor = cursor;
            Camera = new KitMapCamera(KitUnity.Vector(root.rect.size));
        }

        /// <summary>Top-left positioned map view, null headless, before the graphical kit is active or without a parent.</summary>
        public static KitMapView Create(RectTransform parent, string name, Vector2 at, Vector2 size)
        {
            if (!CrucibleUiPlugin.Active || !MeadworksHeadless.AllowsUi || parent == null) return null;
            var root = Child(parent, name);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
            root.anchoredPosition = at;
            root.sizeDelta = new Vector2(Mathf.Max(32, size.x), Mathf.Max(32, size.y));
            var frame = root.gameObject.AddComponent<Image>();
            frame.color = KitUnity.Colour(KitMapLayout.Frame);
            frame.raycastTarget = true;
            root.gameObject.AddComponent<RectMask2D>();
            var map = Corner(Child(root, "Map")).gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            map.enabled = false;
            var fog = Corner(Child(root, "Fog")).gameObject.AddComponent<RawImage>();
            fog.raycastTarget = false;
            fog.enabled = false;
            var pinLayer = Stretch(Child(root, "Pins"));
            var ring = Outline(root, "Selection", KitMapLayout.SelectionSize, KitUnity.Colour(KitMapLayout.Selection), 2);
            var cross = Outline(root, "PadCursor", 12, KitUnity.Colour(KitMapLayout.Cursor), 2);
            var view = new KitMapView(root, map, fog, pinLayer, ring, cross);
            root.gameObject.AddComponent<KitMapInput>().View = view;
            view.Refresh();
            return view;
        }

        /// <summary>The map texture (the game's large-map image) and an optional fog overlay drawn over the same region.</summary>
        public void SetMap(Texture texture, Texture fogTexture = null)
        {
            if (disposed) return;
            map.texture = texture; map.enabled = texture != null;
            fog.texture = fogTexture; fog.enabled = fogTexture != null;
            Refresh();
        }

        /// <summary>Explored mask over map points; null shows every pin. A throwing mask hides the pin.</summary>
        public void SetExplored(Func<KitVector, bool> mask) { explored = mask; Refresh(); }

        /// <summary>Replaces every pin; the selection is kept when its index still exists.</summary>
        public void SetPins(IReadOnlyList<KitMapPin> next)
        {
            if (disposed) return;
            pins.Clear(); points.Clear();
            if (next != null) foreach (var pin in next) { pins.Add(pin); points.Add(KitUnity.Vector(pin.At)); }
            if (Selected >= pins.Count) Selected = -1;
            Refresh();
        }

        /// <summary>Moves one pin (for example the player arrow each frame) without rebuilding the others.</summary>
        public void MovePin(int index, Vector2 at, float rotation)
        {
            if (disposed || index < 0 || index >= pins.Count) return;
            var pin = pins[index]; pin.At = at; pin.Rotation = rotation; pins[index] = pin; points[index] = KitUnity.Vector(at);
            Place(index);
            if (index == Selected) PlaceRing();
        }

        /// <summary>Selects a pin (-1 clears) without calling <see cref="OnSelect"/>; <paramref name="centre"/> pans to it.</summary>
        public void Select(int index, bool centre = false)
        {
            if (disposed) return;
            Selected = index >= 0 && index < pins.Count ? index : -1;
            if (centre && Selected >= 0) Camera.CenterOn(points[Selected]);
            Refresh();
        }

        /// <summary>
        /// One pad frame from the product (it decides focus and that nothing reacts while typing): left stick moves the
        /// cursor, the trigger axis zooms about it (positive in), <paramref name="select"/> (A) picks the pin under it.
        /// </summary>
        public void Pad(Vector2 stick, float zoomAxis, bool select, float deltaTime)
        {
            if (disposed) return;
            Camera.MoveCursor(KitUnity.Vector(stick), deltaTime);
            Camera.ZoomCursor(zoomAxis, deltaTime);
            cursor.gameObject.SetActive(true);
            if (select) Pick(Camera.Cursor, KitMapLayout.PadHitRadius);
            Refresh();
        }

        /// <summary>Re-reads the view size and places the map image, pins, selection ring and pad cursor.</summary>
        public void Refresh()
        {
            if (disposed || Root == null) return;
            var size = KitUnity.Vector(Root.rect.size);
            if (size != Camera.ViewSize && size.X >= 1 && size.Y >= 1) Camera.Resize(size);
            var image = KitUnity.Rect(Camera.ImageRect);
            var uv = KitUnity.Rect(Camera.VisibleMap);
            foreach (var raw in new[] { map, fog })
            {
                var rect = raw.rectTransform;
                rect.anchoredPosition = image.position;
                rect.sizeDelta = image.size;
                raw.uvRect = uv;
            }
            while (pool.Count < pins.Count) pool.Add(NewPin(pool.Count));
            for (int i = 0; i < pool.Count; i++)
                if (i < pins.Count) Place(i);
                else pool[i].root.gameObject.SetActive(false);
            PlaceRing();
            cursor.anchoredPosition = KitUnity.Vector(Camera.Cursor);
        }

        internal bool Shows(int index) => index >= 0 && index < pins.Count && KitMapRules.Shows(points[index], pins[index].AlwaysShow, explored);

        internal void Pick(KitVector view, float radius)
        {
            int hit = Camera.HitTest(view, points, Shows, radius);
            if (hit < 0) return;
            Selected = hit;
            Refresh();
            try { OnSelect?.Invoke(hit); }
            catch (Exception e) { CrucibleUiPlugin.Log?.Error($"Crucible.UI: map selection on {Root.name} failed: {e}"); }
        }

        internal void HidePadCursor() { if (!disposed) cursor.gameObject.SetActive(false); }

        void Place(int index)
        {
            var (root, icon, label) = pool[index];
            var pin = pins[index];
            var at = Camera.MapToView(points[index]);
            float size = pin.Size > 0 ? pin.Size : KitMapLayout.PinSize;
            bool shown = Shows(index) && Camera.InView(at, size);
            root.gameObject.SetActive(shown);
            if (!shown) return;
            root.anchoredPosition = KitUnity.Vector(at);
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            icon.rectTransform.localEulerAngles = new Vector3(0, 0, pin.Rotation);
            icon.sprite = pin.Icon; icon.enabled = pin.Icon != null; icon.color = pin.Tint;
            label.text = pin.Label ?? "";
            label.gameObject.SetActive(labels && !string.IsNullOrEmpty(pin.Label));
            label.rectTransform.anchoredPosition = new Vector2(0, -(size / 2 + KitMapLayout.LabelGap));
        }

        void PlaceRing()
        {
            bool shown = Selected >= 0 && Selected < pins.Count && Shows(Selected);
            ring.gameObject.SetActive(shown);
            if (shown) ring.anchoredPosition = KitUnity.Vector(Camera.MapToView(points[Selected]));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            OnSelect = null;
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
        }
    }
}
