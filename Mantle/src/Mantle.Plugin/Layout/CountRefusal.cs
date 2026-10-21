// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Layout
{
    /// <summary>
    /// The user, 2026-10-12: a lower Food, Mead or Ammo count whose removed slots hold items with no free cell to go to is
    /// refused (<see cref="CountGuard"/>): the old count is set back when it is a fixed setting, a notice asks for room,
    /// and the layout is left as it is. Nothing drops.
    /// </summary>
    public static class CountRefusal
    {
        public static bool Refused(Player player)
        {
            InventoryLayout from = LayoutRecord.Read(player);
            List<SlotKind> lowered = CountGuard.Blocked(player.GetInventory().GetAllItems(), from, LayoutBuilder.Wanted(player));
            if (lowered.Count == 0)
                return false;
            foreach (SlotKind kind in lowered)
                Restore(kind, CountGuard.Count(from, kind));
            player.Message(MessageHud.MessageType.Center, Accessories.AccessoryText.Text("count_noroom"));
            return true;
        }

        private static void Restore(SlotKind kind, int count)
        {
            if (kind == SlotKind.Food && !InventorySettings.FoodSlotsFollowEating.Value)
                InventorySettings.FoodSlots.Value = count;
            else if (kind == SlotKind.Mead)
                InventorySettings.MeadSlots.Value = count;
            else if (kind == SlotKind.Ammo)
                InventorySettings.AmmoSlots.Value = count;
        }
    }
}
