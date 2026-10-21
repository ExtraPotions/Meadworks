// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// AX02: original pooling/variable-height/focus extension over the existing adopted SheetScroll factory.
// Reference behavior: PackPanel 0.12.0 Panels/{SheetScroll,SheetInputs}.cs at
// 01821b8557e4a77e37f5433c885b1f122cae8249. SheetInputs supplies the changed-input fingerprint
// principle; upstream has no virtual list implementation. No new scroll/widget framework is copied.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Stable identity, measured height and binding for a reusable row. Bind must not add duplicate handlers.</summary>
    public sealed class KitVirtualRow
    {
        public string Key { get; }
        public float Height { get; }
        public Action<RectTransform> Bind { get; }
        public Action<RectTransform> Unbind { get; }
        public KitVirtualRow(string key, float height, Action<RectTransform> bind, Action<RectTransform> unbind = null)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A row requires a stable key.", nameof(key));
            if (height <= 0 || float.IsNaN(height) || float.IsInfinity(height)) throw new ArgumentOutOfRangeException(nameof(height));
            Key = key; Height = height; Bind = bind ?? throw new ArgumentNullException(nameof(bind)); Unbind = unbind;
        }
    }

    /// <summary>SheetScroll with variable-height rows pooled to the visible range, plus an offscreen focused row.</summary>
    public sealed class KitVirtualList : IDisposable
    {
        sealed class Bound
        {
            internal RectTransform Rect;
            internal KitVirtualRow Model;
            internal bool Dirty;
        }
        public RectTransform Root { get; }
        public RectTransform Content { get; }
        public ScrollRect Scroll { get; }
        public int Count => rows.Count;
        public int AllocatedRows => live.Count + pool.Count;
        public string FocusedKey { get; private set; }
        readonly Func<RectTransform, RectTransform> factory;
        readonly Dictionary<string, Bound> live = new Dictionary<string, Bound>(StringComparer.Ordinal);
        readonly Stack<Bound> pool = new Stack<Bound>();
        readonly Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<KitVirtualRow> rows = new List<KitVirtualRow>();
        readonly HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> removed = new List<string>();
        float[] offsets = new float[] { 0 };
        long revision;
        bool initialized, disposed, refreshing;

        KitVirtualList(RectTransform root, RectTransform content, Func<RectTransform, RectTransform> factory)
        {
            Root = root; Content = content; this.factory = factory;
            Scroll = content.GetComponentInParent<ScrollRect>(true);
            Scroll.onValueChanged.AddListener(Scrolled);
        }
        public static KitVirtualList Create(Transform parent, string name, Vector2 size, Func<RectTransform, RectTransform> rowFactory)
        {
            if (rowFactory == null) throw new ArgumentNullException(nameof(rowFactory));
            if (size.x <= 0 || size.y <= 0 || float.IsNaN(size.x) || float.IsNaN(size.y) || float.IsInfinity(size.x) || float.IsInfinity(size.y))
                throw new ArgumentOutOfRangeException(nameof(size));
            if (!CrucibleUiPlugin.Active || parent == null) return null;
            var node = new GameObject(name, typeof(RectTransform));
            node.layer = parent.gameObject.layer;
            var root = (RectTransform)node.transform; root.SetParent(parent, false); root.sizeDelta = size;
            if (KitForged.InWindow(parent)) KitForged.Inset(root);
            var content = SheetScroll.Make(root, KitRowLayout.ScrollBarWidth * 2, 0);
            return new KitVirtualList(root, content, rowFactory);
        }

        /// <summary>Revision fingerprints displayed facts, as SheetInputs does. Unchanged revision/shape does not rebind.</summary>
        public void SetRows(IReadOnlyList<KitVirtualRow> source, long revision)
        {
            if (disposed) return;
            if (source == null) throw new ArgumentNullException(nameof(source));
            var nextOffsets = new float[source.Count + 1];
            var nextIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            bool same = initialized && this.revision == revision && source.Count == rows.Count;
            for (int i = 0; i < source.Count; i++)
            {
                var row = source[i] ?? throw new ArgumentException("Null row.", nameof(source));
                if (nextIndex.ContainsKey(row.Key)) throw new ArgumentException("Duplicate stable row key: " + row.Key, nameof(source));
                nextIndex.Add(row.Key, i); nextOffsets[i + 1] = nextOffsets[i] + row.Height;
                if (float.IsInfinity(nextOffsets[i + 1])) throw new ArgumentOutOfRangeException(nameof(source), "Content height overflow.");
                same &= i < rows.Count && rows[i].Key == row.Key && rows[i].Height == row.Height;
            }
            if (same) return;
            string path = CaptureFocus();
            int oldFocusIndex = FocusedKey != null && index.TryGetValue(FocusedKey, out int focused) ? focused : 0;
            float oldY = Content.anchoredPosition.y;
            int anchorIndex = At(oldY); string anchor = anchorIndex < rows.Count ? rows[anchorIndex].Key : null;
            float delta = oldY - offsets[anchorIndex];
            rows.Clear(); for (int i = 0; i < source.Count; i++) rows.Add(source[i]);
            index.Clear(); foreach (var pair in nextIndex) index.Add(pair.Key, pair.Value);
            offsets = nextOffsets; this.revision = revision; initialized = true;
            if (FocusedKey != null && !index.ContainsKey(FocusedKey))
                FocusedKey = rows.Count > 0 ? rows[Mathf.Min(oldFocusIndex, rows.Count - 1)].Key : null;
            foreach (var bound in live.Values) bound.Dirty = true;
            Content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, offsets[rows.Count]);
            if (anchor != null && index.TryGetValue(anchor, out int at)) oldY = offsets[at] + delta;
            Position(oldY); Refresh(); RestoreFocus(path);
        }
        /// <summary>Call with the product window tick; catches viewport resizing and focus changes without rebuilding data.</summary>
        public void Tick() { if (disposed || Root == null) return; CaptureFocus(); Refresh(); }
        public void Focus(string key)
        {
            if (disposed || key == null || !index.TryGetValue(key, out int at)) return;
            FocusedKey = key;
            float y = Content.anchoredPosition.y, height = Scroll.viewport.rect.height;
            if (offsets[at] < y) y = offsets[at];
            else if (offsets[at + 1] > y + height) y = offsets[at + 1] - height;
            Position(y); Refresh(); RestoreFocus("");
        }
        void Position(float y)
        {
            Scroll.StopMovement(); var position = Content.anchoredPosition;
            position.y = Mathf.Clamp(y, 0, Mathf.Max(0, offsets[rows.Count] - Scroll.viewport.rect.height));
            Content.anchoredPosition = position;
        }
        int At(float y)
        {
            int low = 0, high = rows.Count;
            while (low < high) { int mid = low + (high - low) / 2; if (offsets[mid + 1] <= y) low = mid + 1; else high = mid; }
            return low;
        }
        void Scrolled(Vector2 _) => Refresh();
        void Refresh()
        {
            if (disposed || refreshing || Content == null) return;
            refreshing = true;
            try
            {
                wanted.Clear();
                int first = Mathf.Max(0, At(Mathf.Max(0, Content.anchoredPosition.y)) - 1);
                int last = Mathf.Min(rows.Count - 1, At(Content.anchoredPosition.y + Scroll.viewport.rect.height) + 1);
                for (int i = first; i <= last; i++) wanted.Add(rows[i].Key);
                if (FocusedKey != null && index.ContainsKey(FocusedKey)) wanted.Add(FocusedKey);
                removed.Clear(); foreach (var key in live.Keys) if (!wanted.Contains(key)) removed.Add(key);
                foreach (var key in removed) { var row = live[key]; live.Remove(key); Release(row); pool.Push(row); }
                foreach (var key in wanted)
                {
                    int i = index[key];
                    if (!live.TryGetValue(key, out var bound))
                    {
                        bound = pool.Count > 0 ? pool.Pop() : new Bound { Rect = factory(Content) };
                        if (bound.Rect == null) throw new InvalidOperationException("The virtual row factory returned no owned rect.");
                        bound.Rect.SetParent(Content, false); bound.Dirty = true; live.Add(key, bound);
                    }
                    var rect = bound.Rect; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1);
                    rect.sizeDelta = new Vector2(0, rows[i].Height); rect.anchoredPosition = new Vector2(0, -offsets[i]);
                    rect.SetSiblingIndex(i); rect.gameObject.SetActive(true);
                    if (bound.Dirty)
                    {
                        if (!ReferenceEquals(bound.Model, rows[i]))
                        {
                            Unbind(bound); bound.Model = rows[i];
                        }
                        bound.Model.Bind(rect); bound.Dirty = false;
                    }
                }
            }
            finally { refreshing = false; }
        }
        string CaptureFocus()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) { FocusedKey = null; return null; }
            foreach (var pair in live)
            {
                var rect = pair.Value.Rect;
                if (rect == null || !selected.transform.IsChildOf(rect)) continue;
                FocusedKey = pair.Key;
                string path = ""; var node = selected.transform;
                while (node != rect) { path = path.Length == 0 ? node.name : node.name + "/" + path; node = node.parent; }
                return path;
            }
            FocusedKey = null; return null;
        }
        void RestoreFocus(string path)
        {
            if (path == null || FocusedKey == null || EventSystem.current == null || !live.TryGetValue(FocusedKey, out var bound)) return;
            var child = path.Length > 0 ? bound.Rect.Find(path) : bound.Rect;
            var selectable = child != null ? child.GetComponent<Selectable>() : null;
            if (selectable == null || !selectable.IsInteractable())
                foreach (var next in bound.Rect.GetComponentsInChildren<Selectable>(false))
                    if (next.IsInteractable()) { selectable = next; break; }
            EventSystem.current.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
        }
        static void Unbind(Bound bound)
        {
            try { if (bound.Rect != null) bound.Model?.Unbind?.Invoke(bound.Rect); }
            catch (Exception error) { CrucibleUiPlugin.Log?.Error("Crucible.UI: virtual row cleanup failed: " + error); }
            finally { bound.Model = null; }
        }
        void Release(Bound bound)
        {
            Unbind(bound); bound.Dirty = true;
            if (bound.Rect != null) bound.Rect.gameObject.SetActive(false);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (Scroll != null) Scroll.onValueChanged.RemoveListener(Scrolled);
            foreach (var bound in live.Values) Release(bound);
            live.Clear(); pool.Clear(); rows.Clear(); index.Clear(); FocusedKey = null;
            if (Root != null) { Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(Root.gameObject); }
        }
    }
}
