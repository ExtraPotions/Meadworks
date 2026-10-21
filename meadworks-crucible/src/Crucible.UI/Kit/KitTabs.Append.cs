// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Native tab cloning/placement adapted from OpenKeep SalvageTab.
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later.
// Reference ad278ecf323901c3645eb1f9f6fac6833a0f8cb1; Changed by expDARE, 2026: shared owned kit tab.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitTabs
    {
        /// <summary>A visible custom tab currently owns this native tab row.</summary>
        public static bool HasSelected(RectTransform row) => KitGameTab.HasSelected(row);

        /// <summary>The native game leaves the previous caption when no recipe is selected.</summary>
        public static void RestoreEmptyRecipeCaption(InventoryGui gui, bool hasRecipe)
        {
            if (gui == null || hasRecipe || gui.m_tabCraft == null || gui.m_tabUpgrade == null
                || HasSelected(gui.m_tabCraft.transform.parent as RectTransform) || Player.m_localPlayer == null
                || Localization.instance == null || gui.m_craftButton == null) return;
            var station = Player.m_localPlayer.GetCurrentCraftingStation();
            string key = station != null && station.m_upgrader ? "$inventory_upgraderbutton"
                : !gui.m_tabUpgrade.interactable ? "$inventory_upgradebutton" : "$inventory_craftbutton";
            var label = gui.m_craftButton.GetComponentInChildren<TMP_Text>(true);
            string text = Localization.instance.Localize(key);
            if (label != null && label.text != text) label.text = text;
        }

        /// <summary>Append a native tab after the currently visible tabs; preserve their own appearance and handlers.</summary>
        public static KitGameTab Append(RectTransform row, Button template, string name, string caption, Action selected)
        {
            if (!CrucibleUiPlugin.Active || row == null || template == null) return null;
            return new KitGameTab(row, template, name, caption, selected);
        }
    }

    public sealed class KitGameTab : IDisposable
    {
        static readonly List<KitGameTab> registered = new List<KitGameTab>();
        readonly RectTransform row;
        readonly List<(Button Button, UnityAction Listener)> listeners = new List<(Button, UnityAction)>();
        readonly Action selected;
        public Button Button { get; }
        public bool Selected { get; private set; }
        bool disposed;
        bool blocked;

        internal static bool HasSelected(RectTransform row) => row != null && registered.Exists(tab =>
            tab.row == row && tab.Selected && tab.Button != null && tab.Button.gameObject.activeInHierarchy);

        internal KitGameTab(RectTransform row, Button template, string name, string caption, Action selected)
        {
            this.row = row;
            this.selected = selected;
            var clone = UnityEngine.Object.Instantiate(template.gameObject, row);
            clone.name = name;
            Button = clone.GetComponent<Button>();
            Button.onClick = new Button.ButtonClickedEvent();
            foreach (var localizer in clone.GetComponentsInChildren<Localize>(true)) UnityEngine.Object.Destroy(localizer);
            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = caption ?? "";
            Button.onClick.AddListener(() =>
            {
                if (disposed || blocked || Selected) return;
                Select(true);
                try { selected?.Invoke(); }
                catch (Exception error) { CrucibleUiPlugin.Log?.Warning("The " + (caption ?? "") + " tab failed to open (" + error.GetType().Name + "). Report it with this log."); }
            });
            foreach (var sibling in row.GetComponentsInChildren<Button>(true))
            {
                if (sibling == Button || sibling.transform.parent != row) continue;
                UnityAction handler = () => Select(false);
                sibling.onClick.AddListener(handler);
                listeners.Add((sibling, handler));
            }
            registered.Add(this);
            Refresh();
        }

        public void Select(bool selected)
        {
            if (disposed) return;
            if (selected)
                foreach (var other in registered)
                    if (other != this && other.row == row) other.Select(false);
            Selected = selected;
            Button.interactable = !selected && !blocked;
        }

        public void Block(BlockedOption option)
        {
            blocked = option != null && !option.Allowed;
            KitButton.Block(Button, option);
            Button.interactable = !Selected && !blocked;
        }

        public void Refresh()
        {
            if (disposed || Button == null) return;
            var native = new List<RectTransform>();
            foreach (Transform child in row)
                if (child.GetComponent<Button>() != null && child is RectTransform rect
                    && !registered.Exists(tab => tab.row == row && tab.Button != null && tab.Button.transform == child))
                    native.Add(rect);
            if (native.Count == 0) return;
            // Custom tabs must not become each other's moving layout anchor.
            Vector2 step = native.Count > 1 ? native[1].anchoredPosition - native[0].anchoredPosition : new Vector2(native[0].rect.width, 0);
            if (step.sqrMagnitude < 1) step = new Vector2(native[native.Count - 1].rect.width, 0);
            var last = native.FindLast(rect => rect.gameObject.activeSelf);
            if (last == null) return;
            Vector2 position = last.anchoredPosition;
            foreach (Transform child in row)
                if (child.gameObject.activeSelf && registered.Exists(tab => tab.row == row && tab.Button != null && tab.Button.transform == child))
                {
                    position += step;
                    ((RectTransform)child).anchoredPosition = position;
                }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            registered.Remove(this);
            foreach (var pair in listeners) if (pair.Button != null) pair.Button.onClick.RemoveListener(pair.Listener);
            listeners.Clear();
            if (Button != null) KitUnity.Retire(Button.gameObject);
        }
    }
}
