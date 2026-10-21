// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: one square in the HUD column right of the minimap (user decision 2026-10-12 14:54). Moved here from the
// identical copies in Cairn and Keel (their ColumnSquare); the square is now made by the column's dresser through the
// meadworks.hud.column_square capability instead of copying an object found by Mantle's names (user decision 22:41).
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible.UI.PlateColumn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using Object = UnityEngine.Object;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A square in the HUD column right of the minimap (<c>PlateColumn_hudboxes</c>), in the look of the product that
    /// dresses the column (Mantle), named by rank as the column's boxes are so every product sorts it the same way, showing
    /// its own icon, value and tooltip. Without the column or a dresser nothing is drawn; a hidden square leaves no gap.
    /// </summary>
    public sealed class KitColumnSquare
    {
        const string Prefix = "PlateColumn_plate_";
        static readonly List<Transform> members = new List<Transform>();
        static readonly CapabilityLink<Func<object, Tuple<object, object, object, object>>> dresser =
            new CapabilityLink<Func<object, Tuple<object, object, object, object>>>(HudColumnSquareContract.Id, HudColumnSquareContract.ContractVersion);
        readonly string name;
        RectTransform? rect, column;
        Image? icon;
        TMP_Text? text;
        UITooltip? tip;
        float nextTry;

        /// <param name="rank">Column rank, lower sits higher (Mantle's armour 100, carry 300, world tier 400; Cairn's comfort 500, Keel's wind 600, Solstice's season 700).</param>
        /// <param name="id">The square's id, unique in the column.</param>
        public KitColumnSquare(int rank, string id) => name = Prefix + rank.ToString("D4") + "_" + id;

        public RectTransform? Rect => rect;
        public Image? Icon => icon;

        /// <summary>Shows the square with this icon (null keeps the current one), value and tooltip, or hides it.</summary>
        public void Draw(bool show, Sprite? sprite, string value, string topic, string tooltip)
        {
            var small = Minimap.instance != null ? Minimap.instance.m_smallRoot : null;
            var found = small != null ? small.transform.Find(HudContainer.Name) as RectTransform : null;
            if (column != found || (column != null && rect == null)) Dispose();
            if (!show || found == null) { Hide(); return; }
            if (rect == null && !Make(found)) return;
            if (!rect!.gameObject.activeSelf) rect.gameObject.SetActive(true);
            if (sprite != null && icon!.sprite != sprite) icon.sprite = sprite;
            if (text!.text != value) text.text = value;
            tip!.m_topic = topic;
            tip.m_text = tooltip;
        }

        public void Hide()
        {
            if (rect != null && rect.gameObject.activeSelf) rect.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (rect != null) Object.Destroy(rect.gameObject);
            rect = column = null; icon = null; text = null; tip = null;
        }

        bool Make(RectTransform found)
        {
            if (Time.unscaledTime < nextTry) return false;
            nextTry = Time.unscaledTime + 1f;
            var existing = found.Find(name) as RectTransform;
            if (existing != null) Object.Destroy(existing.gameObject);
            var registry = CruciblePlugin.IsReady ? CruciblePlugin.Services.Capabilities : null;
            // The dresser answers null until its own square is dressed; copying earlier would copy the undressed look.
            if (!dresser.TryGet(registry, out var make) || !HudColumnSquareContract.TryMake(make, found, out var made)) return false;
            var go = (made!.Item1 as RectTransform)?.gameObject;
            var picture = made.Item2 as Image;
            var label = made.Item3 as TMP_Text;
            var background = made.Item4 as Image;
            var gui = InventoryGui.instance;
            var hover = go != null && gui != null ? KitTooltip.Attach(go) : null;
            // The tooltip is the column's gold-bordered item tooltip; without its prefab a hover would fail, so no square yet.
            if (go == null || go.transform.parent != found || label == null || background == null || picture == null || hover == null
                || !Column.DressTooltip(gui!, hover)) { if (go != null) Object.Destroy(go); return false; }
            go.name = name;
            column = found; rect = (RectTransform)go.transform; icon = picture; text = label;
            icon.rectTransform.localRotation = Quaternion.identity;
            // Two lines fit in the box, as on Mantle's carry square.
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, 34f);
            text.richText = false;
            text.text = "";
            background.raycastTarget = true;
            tip = hover;
            Sort(found);
            return true;
        }

        /// <summary>Top to bottom by name, as the column orders its boxes: the rank leads the name.</summary>
        static void Sort(RectTransform found)
        {
            members.Clear();
            foreach (Transform child in found) if (child.name.StartsWith(Prefix, StringComparison.Ordinal)) members.Add(child);
            members.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            for (var i = 0; i < members.Count; i++) if (members[i].GetSiblingIndex() != i) members[i].SetSiblingIndex(i);
            members.Clear();
            LayoutRebuilder.ForceRebuildLayoutImmediate(found);
        }
    }
}
