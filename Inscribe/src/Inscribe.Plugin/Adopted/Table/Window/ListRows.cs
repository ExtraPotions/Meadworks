// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>One row of the window's list: what it shows and what a click does.</summary>
    internal readonly struct ListRow
    {
        public ListRow(Sprite? icon, string name, string? badge, float? durability, bool selected, bool dim, Action click,
            ItemDrop.ItemData? item = null)
        {
            Item = item;
            Icon = icon;
            Name = name;
            Badge = badge;
            Durability = durability;
            Selected = selected;
            Dim = dim;
            Click = click;
        }

        /// <summary>The player's item the row shows, for its icon's rarity backdrop, seal and sockets; null for none.</summary>
        public ItemDrop.ItemData? Item { get; }

        public Sprite? Icon { get; }
        public string Name { get; }
        public string? Badge { get; }
        public float? Durability { get; }
        public bool Selected { get; }
        public bool Dim { get; }
        public Action Click { get; }
    }

    /// <summary>
    /// The window's list, made of the game's own recipe rows (<c>InventoryGui.m_recipeElementPrefab</c>: icon, name,
    /// durability bar, the small number in the corner, the selected frame), stacked as the game stacks its recipes.
    /// </summary>
    internal sealed class ListRows
    {
        private static readonly Color Grey = new Color(0.66f, 0.66f, 0.66f, 1f);

        private readonly GameObject _template;
        private readonly RectTransform? _root;
        private readonly float _space;
        private readonly float _baseHeight;
        private readonly List<GameObject> _rows = new List<GameObject>();

        public ListRows(GameObject template, ScrollRect? scroll, float space)
        {
            _template = template;
            _root = scroll != null ? scroll.content : null;
            _space = space;
            _baseHeight = scroll != null ? ((RectTransform)scroll.transform).rect.height : 0f;
        }

        public void Fill(IReadOnlyList<ListRow> rows)
        {
            foreach (GameObject row in _rows)
            {
                Object.Destroy(row);
            }
            _rows.Clear();
            if (_root == null)
            {
                return;
            }
            foreach (ListRow row in rows)
            {
                _rows.Add(Make(row, _rows.Count));
            }
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(_baseHeight, _rows.Count * _space));
        }

        private GameObject Make(ListRow row, int index)
        {
            GameObject element = Object.Instantiate(_template, _root);
            element.SetActive(true);
            ((RectTransform)element.transform).anchoredPosition = new Vector2(0f, index * -_space);
            Image? icon = element.transform.Find("icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = row.Icon;
                icon.color = row.Dim ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                Display.Backdrops.IconBackdrop.Set(icon, row.Item);
            }
            TMP_Text? name = element.transform.Find("name")?.GetComponent<TMP_Text>();
            if (name != null)
            {
                name.text = row.Name;
                name.color = row.Dim ? Grey : Color.white;
            }
            Decorate(element.transform, row);
            Action click = row.Click;
            element.GetComponent<Button>()?.onClick.AddListener(() => click());
            return element;
        }

        private static void Decorate(Transform element, ListRow row)
        {
            GuiBar? bar = element.Find("Durability")?.GetComponent<GuiBar>();
            if (bar != null)
            {
                bar.gameObject.SetActive(row.Durability.HasValue);
                bar.SetValue(row.Durability ?? 0f);
            }
            TMP_Text? badge = element.Find("QualityLevel")?.GetComponent<TMP_Text>();
            if (badge != null)
            {
                badge.gameObject.SetActive(row.Badge != null);
                badge.text = row.Badge ?? "";
            }
            element.Find("selected")?.gameObject.SetActive(row.Selected);
        }
    }
}
