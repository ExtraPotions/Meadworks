// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// A scrolling list of copies of the compendium's own list entry (a button with its background, its "selected"
    /// highlight and its text), one row under the other. Clicking a row reports its index; one row can be marked, and a
    /// marked row is scrolled into view on request.
    /// </summary>
    internal sealed class ElementList
    {
        private readonly RectTransform _root;
        private readonly GameObject _template;
        private readonly ScrollRect _scroll;
        private readonly float _height;
        private readonly float _spacing;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<GameObject?> _marks = new List<GameObject?>();
        private int _marked = -1;

        public Action<int>? Clicked;

        public ElementList(RectTransform root, GameObject template, ScrollRect scroll, float height, float spacing)
        {
            _root = root;
            _template = template;
            _scroll = scroll;
            _height = height;
            _spacing = spacing;
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.offsetMin = new Vector2(0f, root.offsetMin.y);
            root.offsetMax = new Vector2(-ScrollbarRoom, 0f);
        }

        /// <summary>The rows end this far short of the list's right edge, clear of its scrollbar.</summary>
        private const float ScrollbarRoom = 12f;

        public int Count => _rows.Count;

        public void Clear()
        {
            foreach (GameObject row in _rows)
            {
                row.SetActive(false);
                UnityEngine.Object.Destroy(row);
            }
            _rows.Clear();
            _marks.Clear();
            _marked = -1;
            Resize();
        }

        /// <summary>A new row at the bottom; its text is the entry's "name" text, at <paramref name="size"/>.</summary>
        public GameObject Add(string text, float size)
        {
            int index = _rows.Count;
            GameObject row = UnityEngine.Object.Instantiate(_template, _root, false);
            row.name = "ecr_recap_row";
            row.SetActive(true);
            RectTransform rect = (RectTransform)row.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * _spacing);
            rect.sizeDelta = new Vector2(0f, _height);
            UiParts.Rewire(row.GetComponent<Button>(), () => Clicked?.Invoke(index));
            Label(row, text, size);
            GameObject? mark = Utils.FindChild(row.transform, "selected")?.gameObject;
            mark?.SetActive(false);
            _rows.Add(row);
            _marks.Add(mark);
            Resize();
            return row;
        }

        public void Mark(int index, bool reveal)
        {
            if (index == _marked)
            {
                return;
            }
            SetMark(_marked, false);
            _marked = index;
            SetMark(index, true);
            if (reveal && index >= 0)
            {
                Reveal(index);
            }
        }

        public static TMP_Text? LabelOf(GameObject row) => Utils.FindChild(row.transform, "name")?.GetComponent<TMP_Text>();

        private static void Label(GameObject row, string text, float size)
        {
            TMP_Text? label = LabelOf(row);
            if (label == null)
            {
                return;
            }
            label.enableAutoSizing = false;
            label.fontSize = size;
            label.richText = true;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = text;
        }

        private void SetMark(int index, bool on)
        {
            if (index >= 0 && index < _marks.Count)
            {
                _marks[index]?.SetActive(on);
            }
        }

        private void Resize()
        {
            float view = ((RectTransform)_scroll.transform).rect.height;
            _root.sizeDelta = new Vector2(_root.sizeDelta.x, Mathf.Max(view, _rows.Count * _spacing));
        }

        // Scrolls just enough that the row is wholly in view.
        private void Reveal(int index)
        {
            float view = ((RectTransform)_scroll.transform).rect.height;
            float extra = _root.rect.height - view;
            if (extra <= 0f)
            {
                return;
            }
            float top = index * _spacing;
            float offset = (1f - _scroll.verticalNormalizedPosition) * extra;
            offset = Mathf.Clamp(offset, top + _height - view, top);
            _scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / extra);
        }
    }
}
