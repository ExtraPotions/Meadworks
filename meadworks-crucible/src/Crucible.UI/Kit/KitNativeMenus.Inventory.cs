// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        static readonly InventoryAction[] inventoryActions =
        {
            new InventoryAction("takeAll", gui => gui.m_takeAllButton),
            new InventoryAction("stackAll", gui => gui.m_stackAllButton),
            new InventoryAction("drop", gui => gui.m_dropButton),
            new InventoryAction("craft", gui => gui.m_craftButton),
            new InventoryAction("cancel", gui => gui.m_craftCancelButton),
            new InventoryAction("repair", gui => gui.m_repairButton),
            new InventoryAction("variant", gui => gui.m_variantButton)
        };
        static InventoryGui inventoryHost;

        static void TickInventory()
        {
            if (!MeadworksHeadless.AllowsUi) { ShutdownInventory(); return; }
            var gui = InventoryGui.instance;
            if (gui == null) { ShutdownInventory(); return; }
            if (!ReferenceEquals(gui, inventoryHost))
            {
                ShutdownInventory();
                inventoryHost = gui;
            }
            foreach (var action in inventoryActions) action.Refresh(gui, On);
        }
        static void ShutdownInventory()
        {
            foreach (var action in inventoryActions) action.Dispose();
            inventoryHost = null;
        }
        internal static object InventoryStatus
        {
            get
            {
                var rows = new object[inventoryActions.Length];
                for (int i = 0; i < rows.Length; i++)
                {
                    var action = inventoryActions[i];
                    rows[i] = new { id = action.Id, button = action.Button != null ? action.Button.name : "",
                        captured = action.Captured, yielded = action.Yielded, labelCandidates = action.LabelCandidates };
                }
                return new { present = inventoryHost != null, on = On, actions = rows };
            }
        }
        sealed class InventoryAction : IDisposable
        {
            internal readonly string Id;
            readonly Func<InventoryGui, Button> read;
            internal Button Button { get; private set; }
            internal bool Captured => imageLook != null || textLook != null;
            internal bool Yielded { get; private set; }
            internal int LabelCandidates { get; private set; }
            Graphic originalGraphic;
            ButtonLook imageLook;
            TextButtonLook textLook;
            FontLook fontLook;

            internal InventoryAction(string id, Func<InventoryGui, Button> read) { Id = id; this.read = read; }
            internal void Refresh(InventoryGui gui, bool on)
            {
                var current = read(gui);
                if (!ReferenceEquals(current, Button))
                {
                    Dispose(); Button = current;
                    if (current != null) Capture(gui);
                }
                if (Button == null || Yielded || !Captured) return;
                if (Button.targetGraphic != originalGraphic)
                {
                    Release(); Yielded = true; return;
                }
                imageLook?.Apply(on); textLook?.Apply(on); fontLook?.Apply(on);
            }
            void Capture(InventoryGui gui)
            {
                if (!Native(Button.transform, gui.gameObject)) return;
                foreach (var action in inventoryActions)
                    if (action != this && action.Button == Button && action.Captured) return;
                originalGraphic = Button.targetGraphic;
                if (originalGraphic != null && (!originalGraphic.transform.IsChildOf(Button.transform)
                    || !Native(originalGraphic.transform, gui.gameObject))) return;
                TMP_Text label = originalGraphic as TMP_Text;
                if (label == null)
                {
                    foreach (var candidate in Button.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (candidate.GetComponentInParent<Button>() != Button || !Native(candidate.transform, gui.gameObject)) continue;
                        label = candidate; LabelCandidates++;
                    }
                    if (LabelCandidates != 1) label = null;
                }
                else LabelCandidates = 1;
                if (originalGraphic is Image image)
                {
                    if (owned.Contains(image)) return;
                    imageLook = new ButtonLook(Button, image);
                }
                else if ((originalGraphic == null || originalGraphic is TMP_Text) && label != null && TextButtonLook.Supported)
                    textLook = new TextButtonLook(Button, label);
                if (Captured && label != null) fontLook = new FontLook(label);
            }
            void Release()
            {
                imageLook?.Dispose(); textLook?.Dispose(); fontLook?.Apply(false);
                imageLook = null; textLook = null; fontLook = null;
            }
            public void Dispose()
            {
                Release(); Button = null; originalGraphic = null; Yielded = false; LabelCandidates = 0;
            }
        }
    }
}
