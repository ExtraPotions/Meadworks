using System.Collections.Generic;
using System;
using HarmonyLib;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;
using Mantle.Tackle;
using UnityEngine.UI;
using UnityEngine;

namespace Mantle.Ring
{
    /// <summary>
    /// The gamepad and the player grid's hidden cells. The gamepad moves over the grid's cells by their place in the
    /// inventory, not where they are drawn, so it could stop on a cell nobody sees: a ring cell while the pop-up is shut,
    /// a key not carried, a backpack's blocked cell, a cell after the last slot. A move that lands on one carries on the
    /// same way to the next cell that shows, or stays where it was. A cell of the slot panel's hidden tab counts as shown:
    /// landing there turns the panel to its tab (<see cref="Panels.SlotTabs"/>). While the pop-up is shut the ring button stands in for
    /// the ring: a move onto any ring cell selects the button (the first ring cell underneath), where A or X opens the
    /// pop-up (<see cref="KeyRingClicks"/>) and selects its first key; B then closes the pop-up before it would close the
    /// inventory, and the button is selected again. The tacklebox's cells count as shown only while its pop-up is open
    /// (the box's own slot stays reachable), and B shuts that pop-up the same way (<see cref="TackleGamepad"/>).
    /// </summary>
    public static class KeyRingGamepad
    {
        private static readonly List<int> found = new List<int>();

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGamepad))]
        public static class Move
        {
            [HarmonyPrefix]
            public static void Prefix(InventoryGrid __instance, out Vector2i __state) => __state = __instance.m_selected;

            [HarmonyPostfix]
            public static void Postfix(InventoryGrid __instance, Vector2i __state, bool __runOriginal)
            {
                // A prefix that took the move itself (the loadout row's) skipped this class's prefix too, which leaves __state at
                // (0, 0): the whole-grid settle would read the selection as a jump and steal focus from that prefix's button.
                if (!__runOriginal || __instance.m_selected == __state || !IsPlayerGrid(__instance))
                    return;
                Vector2i settled = Settle(__instance, __state, __instance.m_selected);
                if (settled != __instance.m_selected)
                    Select(__instance, settled);
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
        public static class Back
        {
            [HarmonyPrefix]
            public static void Prefix(InventoryGui __instance)
            {
                bool open = KeyRingState.Open || TackleState.Open;
                if (!open || !ZInput.IsExclusiveGamepadActive() || !InventoryGui.IsVisible() || !NoDialog(__instance))
                    return;
                if (!ZInput.GetButtonDown("JoyButtonB"))
                    return;
                ZInput.ResetButtonStatus("JoyButtonB");
                KeyRingState.Close();
                TackleState.Close();
            }
        }

        /// <summary>Whether the gamepad's selection is on the ring button: a ring cell that the pop-up does not show.</summary>
        public static bool OnButton(InventoryGui gui)
        {
            InventoryGrid grid = gui.m_playerGrid;
            if (!grid.m_uiGroup.IsActive || !ZInput.IsExclusiveGamepadActive())
                return false;
            Slot slot = InventoryState.Layout.SlotAt(grid.m_selected);
            return slot != null && slot.Kind == SlotKind.Key && !(KeyRingState.Open && KeyRingCells.Shows(slot.Number));
        }

        /// <summary>The pop-up opened or closed: the gamepad goes to its first key, or back to the button.</summary>
        public static void Follow(bool open)
        {
            InventoryGrid grid = InventoryGui.instance != null ? InventoryGui.instance.m_playerGrid : null;
            IReadOnlyList<Vector2i> ring = InventoryState.CellsOf(SlotKind.Key);
            if (grid == null || ring.Count == 0 || !KeyRing.Active || !ZInput.IsExclusiveGamepadActive())
                return;
            Slot slot = InventoryState.Layout.SlotAt(grid.m_selected);
            if (!open && slot != null && slot.Kind == SlotKind.Key)
                Select(grid, ring[0]);
            if (!open)
                return;
            KeyRingCells.Found(found);
            if (found.Count > 0)
                Select(grid, ring[found[0] - 1]);
        }

        private static bool IsPlayerGrid(InventoryGrid grid) =>
            InventoryGui.instance != null && grid == InventoryGui.instance.m_playerGrid && InventoryState.Manages(grid.GetInventory());

        /// <summary>From where the move landed, on the same way to the first cell that shows; where it was when none does.</summary>
        private static Vector2i Settle(InventoryGrid grid, Vector2i before, Vector2i landed)
        {
            int dx = Math.Sign(landed.x - before.x);
            int dy = Math.Sign(landed.y - before.y);
            Inventory inventory = grid.GetInventory();
            for (Vector2i pos = landed; pos.x >= 0 && pos.y >= 0 && pos.x < inventory.GetWidth() && pos.y < inventory.GetHeight();
                 pos = new Vector2i(pos.x + dx, pos.y + dy))
            {
                Vector2i target = StandIn(pos);
                if (Shows(target))
                    return target;
            }
            return before;
        }

        /// <summary>While the pop-up is shut, every ring cell stands for the button: the first ring cell.</summary>
        private static Vector2i StandIn(Vector2i pos)
        {
            Slot slot = InventoryState.Layout.SlotAt(pos);
            IReadOnlyList<Vector2i> ring = InventoryState.CellsOf(SlotKind.Key);
            return !KeyRingState.Open && slot != null && slot.Kind == SlotKind.Key && ring.Count > 0 ? ring[0] : pos;
        }

        private static bool Shows(Vector2i pos)
        {
            InventoryLayout layout = InventoryState.Layout;
            if (layout.IsMain(pos))
                return true;
            Slot slot = layout.SlotAt(pos);
            if (slot == null || slot.Kind == SlotKind.Retired)
                return false;
            if (slot.Kind == SlotKind.Tackle)
                return TackleState.Open && TackleCells.Shows(slot.Number);
            if (slot.Kind != SlotKind.Key)
                return true;
            return KeyRingState.Open ? KeyRingCells.Shows(slot.Number) : slot.Number == 1;
        }

        /// <summary>Moves the gamepad's selection to a cell and selects its element (the tacklebox's pop-up uses it too).</summary>
        internal static void Select(InventoryGrid grid, Vector2i pos)
        {
            grid.m_selected = pos;
            RectTransform element = grid.GetGamepadSelectedElement();
            Selectable selectable = element != null && element.gameObject.activeInHierarchy ? element.GetComponent<Selectable>() : null;
            if (selectable != null)
                selectable.Select();
        }

        private static bool NoDialog(InventoryGui gui) =>
            !gui.m_trophiesPanel.activeSelf && !gui.m_achievementsPanel.gameObject.activeSelf && !gui.m_skillsDialog.gameObject.activeSelf
            && !gui.m_textsDialog.gameObject.activeSelf && !gui.m_splitDialog.IsActive && !gui.m_variantDialog.gameObject.activeSelf;
    }
}
