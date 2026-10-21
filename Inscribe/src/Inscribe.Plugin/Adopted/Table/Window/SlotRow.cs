// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// A row of small clickable requirement slots under the description (the runes, the essences), each a copy of the
    /// game's slot scaled down whole, so its frame, icon and text keep the game's proportions. <see cref="Fit"/> shows
    /// fewer of them, smaller when that many do not fit the row at full size (the chisel and eleven gems).
    /// </summary>
    internal sealed class SlotRow
    {
        private const float Gap = 4f;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly GameObject _row;
        private readonly Vector2 _area;
        private readonly Vector2 _size;
        private int _fitted = -1;

        public SlotRow(RectTransform parent, GameObject template, int count, Rect area, Action<int> clicked)
        {
            _row = new GameObject("ECF_SlotRow", typeof(RectTransform));
            _row.transform.SetParent(parent, false);
            Rects.Place((RectTransform)_row.transform, area.x, area.y, area.width, area.height);
            _area = area.size;
            _size = ((RectTransform)template.transform).rect.size;
            for (int i = 0; i < count; i++)
            {
                _slots.Add(Make(template, i, clicked));
            }
            Fit(count);
        }

        public int Count => _slots.Count;

        public Slot this[int index] => _slots[index];

        public void Show(bool on)
        {
            if (_row.activeSelf != on)
            {
                _row.SetActive(on);
            }
        }

        /// <summary>Shows the first <paramref name="count"/> slots spread over the row, shrunk to fit; hides the rest.</summary>
        public void Fit(int count)
        {
            count = Mathf.Clamp(count, 0, _slots.Count);
            if (count == _fitted)
            {
                return;
            }
            _fitted = count;
            float step = Mathf.Min(_area.y + Gap, _area.x / Mathf.Max(1, count));
            float side = Mathf.Min(_area.y, step - Gap);
            float scale = side / Mathf.Max(1f, _size.y);
            for (int i = 0; i < _slots.Count; i++)
            {
                var rect = (RectTransform)_slots[i].Root.transform;
                Rects.Place(rect, i * step, (_area.y - side) / 2f, _size.x, _size.y);
                rect.localScale = new Vector3(scale, scale, 1f);
                _slots[i].Root.SetActive(i < count);
            }
        }

        private Slot Make(GameObject template, int index, Action<int> clicked)
        {
            GameObject copy = Object.Instantiate(template, _row.transform, false);
            copy.name = "ECF_Slot" + index;
            var slot = new Slot(copy);
            slot.Clickable().onClick.AddListener(() => clicked(index));
            slot.Name?.gameObject.SetActive(false);
            // No hover text on the rows (user, 2026-10-07): the copy's tooltip still holds the crafting panel's last material.
            slot.Tooltip("", "");
            return slot;
        }
    }
}
