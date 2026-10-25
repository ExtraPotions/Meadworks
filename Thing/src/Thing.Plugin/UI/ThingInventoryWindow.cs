// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: read-only inventory view. Take and delete are confirmed, then the server moves or removes that one stack.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Thing.Admin;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingInventoryWindow
    {
        static KitWindow window;
        static KitVirtualList list;
        static string playerId = "", playerName = "", armed = "", selected = "";
        static ItemSlot[] slots = Array.Empty<ItemSlot>();
        static int revision;
        static bool vacant = true;
        static Button takeButton, deleteButton;

        internal static void Tick()
        {
            if (window == null) return;
            if (!ThingPanel.CanViewInventory) { Close(); return; }
            if (takeButton != null) takeButton.interactable = ThingPanel.CanMutateInventory;
            if (deleteButton != null) deleteButton.interactable = ThingPanel.CanMutateInventory;
        }

        internal static void Open(string id, string name)
        {
            if (!ThingPanel.CanViewInventory || string.IsNullOrEmpty(id)) return;
            Close();
            playerId = id; playerName = name ?? id; armed = ""; selected = ""; slots = Array.Empty<ItemSlot>();
            window = KitWindow.Create("Thing_Inventory", new Vector2(720, 560));
            if (window == null) return;
            window.Closed += Close;
            var title = KitAlmanacDialog.Label(window.Panel, "Title", ThingPanel.Ui("inventory"), new Vector2(20, -14), new Vector2(680, 32), 22);
            if (KitCanvas.Heading != null) title.font = KitCanvas.Heading;
            KitAlmanacDialog.Label(window.Panel, "Who", playerName, new Vector2(20, -52), new Vector2(680, 28), 16).richText = false;
            list = KitVirtualList.Create(window.Panel, "Items", new Vector2(680, 340), parent =>
            {
                var row = new GameObject("Row", typeof(RectTransform));
                row.transform.SetParent(parent, false);
                return (RectTransform)row.transform;
            });
            if (list != null) KitAlmanacDialog.Place(list.Root, new Vector2(20, -90), new Vector2(680, 340));
            var take = KitButton.Create(window.Panel, "Take", ThingPanel.Ui("inventory_take"), Take, new Vector2(20, -450), new Vector2(210, 40));
            var delete = KitButton.Create(window.Panel, "Delete", ThingPanel.Ui("inventory_delete"), Delete, new Vector2(250, -450), new Vector2(210, 40));
            takeButton = take; deleteButton = delete;
            Tick();
            var close = KitButton.Create(window.Panel, "Close", ThingPanel.Ui("close"), Close, new Vector2(480, -450), new Vector2(210, 40));
            KitTooltip.Set(take.gameObject, ThingPanel.Ui("inventory_take"), ThingPanel.Ui("help_inventory"));
            KitTooltip.Set(delete.gameObject, ThingPanel.Ui("inventory_delete"), ThingPanel.Ui("help_inventory"));
            KitTooltip.Set(close.gameObject, ThingPanel.Ui("close"), ThingPanel.Ui("help_close"));
            window.Focusable(take, 0, 0); window.Focusable(delete, 1, 0); window.Focusable(close, 2, 0);
            window.Open();
            Reload();
        }

        internal static void Close()
        {
            if (armed.Length > 0 && ThingPanel.CanPeople) ThingPanelJobs.Start(new[] { "release", armed }, _ => { }, ThingInventory.OperationId);
            armed = "";
            list?.Dispose(); list = null;
            var old = window; window = null; takeButton = deleteButton = null; slots = Array.Empty<ItemSlot>(); selected = "";
            if (old != null) { old.Closed -= Close; old.Close(); old.Destroy(); }
        }

        static void Reload()
        {
            if (window == null) return;
            ThingPanelJobs.Start(new[] { "view", playerId }, lines =>
            {
                if (window == null) return;
                vacant = lines.Count == 1 && lines[0] == "empty";
                slots = lines.Count > 0 && lines[0] == "items"
                    ? lines.Skip(1).Select(line => ItemSlot.TryParse(line, out var slot) ? slot : default).Where(slot => !string.IsNullOrEmpty(slot.Prefab)).Take(48).ToArray()
                    : Array.Empty<ItemSlot>();
                if (!vacant && (lines.Count == 0 || lines[0] != "items")) ThingPanel.Message(ThingPanel.Ui("inventory_" + (lines.Count > 0 ? lines[0] : "failed")));
                Draw();
            }, ThingInventory.OperationId, () => ThingPanel.Message(ThingPanel.Ui("inventory_failed")));
        }

        static void Draw()
        {
            if (window == null || list == null) return;
            if (slots.Length == 0)
            {
                list.SetRows(Array.Empty<KitVirtualRow>(), ++revision);
                if (vacant) ThingPanel.Message(ThingPanel.Ui("inventory_empty"));
                return;
            }
            var rows = slots.Select((slot, at) => new KitVirtualRow(slot.Text + ":" + at, 56, rect =>
            {
                foreach (Transform child in rect) KitUnity.Retire(child.gameObject);
                var caption = (selected == slot.Text ? "> " : "") + Label(slot);
                var button = KitButton.Create(rect, "Item", caption, () => { selected = slot.Text; Draw(); }, new Vector2(0, -2), new Vector2(660, 52));
                var words = button.GetComponentInChildren<TMP_Text>(true);
                if (words != null) words.richText = false;
                window.Focusable(button, 0, at + 1);
            })).ToArray();
            list.SetRows(rows, ++revision);
        }

        static void Take() => Act(false);
        static void Delete() => Act(true);

        static void Act(bool delete)
        {
            if (window == null || ThingPanelJobs.Busy || !ThingPanel.CanMutateInventory) return;
            if (!ItemSlot.TryParse(selected, out var slot)) { ThingPanel.Message(ThingPanel.Ui("select_item")); return; }
            var body = ThingText.Format(delete ? "ui_inventory_delete_body" : "ui_inventory_take_body", Label(slot), slot.Stack.ToString());
            ThingPanelForms.Confirm(window, body, () =>
            {
                if (!ThingPanel.CanMutateInventory) return;
                if (!delete) Send(new[] { "take", playerId, slot.Text });
                else ThingPanelJobs.Start(new[] { "delete", playerId, slot.Text }, lines =>
                {
                    if (lines.Count == 2 && lines[0] == "armed")
                    {
                        armed = lines[1];
                        ThingPanelForms.Confirm(window, ThingText.Format("ui_inventory_delete_again", Label(slot), slot.Stack.ToString()),
                            () => { var token = armed; armed = ""; Send(new[] { "delete", playerId, slot.Text, token }); },
                            () => { var token = armed; armed = ""; ThingPanelJobs.Start(new[] { "release", token }, _ => { }, ThingInventory.OperationId); });
                    }
                    else Note(lines);
                }, ThingInventory.OperationId, () => ThingPanel.Message(ThingPanel.Ui("inventory_failed")));
            });
        }

        static void Send(string[] args)
        {
            if (!ThingPanel.CanMutateInventory) return;
            ThingPanelJobs.Start(args, Note, ThingInventory.OperationId, () => ThingPanel.Message(ThingPanel.Ui("inventory_failed")));
        }

        static void Note(IReadOnlyList<string> lines)
        {
            var code = lines.Count > 0 ? lines[0] : "failed";
            ThingPanel.Message(ThingPanel.Ui(code == "armed" ? "inventory_busy" : "inventory_" + code));
            if (code == "stored" || code == "dropped" || code == "deleted" || code == "missing") Reload();
        }

        static string Label(ItemSlot slot)
        {
            var name = slot.Prefab;
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(slot.Prefab) : null;
            var item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (item?.m_itemData?.m_shared != null) name = item.m_itemData.m_shared.m_name;
            try { if (global::Localization.instance != null) name = global::Localization.instance.Localize(name); } catch { name = slot.Prefab; }
            return name + " x" + slot.Stack;
        }
    }
}
