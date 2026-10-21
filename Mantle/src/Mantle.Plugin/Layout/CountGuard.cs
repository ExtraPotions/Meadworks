// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using Mantle.Slots;

namespace Mantle.Layout
{
    /// <summary>
    /// The user, 2026-10-12: lowering the Food, Mead or Ammo count never drops or loses an item. When the items in the
    /// removed slots would find no free cell, the change is refused and the old count stays (the caller shows a notice).
    /// </summary>
    public static class CountGuard
    {
        private static readonly SlotKind[] Kinds = { SlotKind.Food, SlotKind.Mead, SlotKind.Ammo };

        /// <summary>The consumable kinds this change lowers, when the move would leave an item without a cell; empty otherwise.</summary>
        public static List<SlotKind> Blocked(IEnumerable<ItemDrop.ItemData> items, InventoryLayout from, InventoryLayout to)
        {
            if (from == null || to == null)
                return new List<SlotKind>();
            List<SlotKind> lowered = Kinds.Where(kind => Count(to, kind) < Count(from, kind)).ToList();
            if (lowered.Count == 0 || LayoutMigration.Plan(items, from, to).Overflow.Count == 0)
                return new List<SlotKind>();
            return lowered;
        }

        public static int Count(InventoryLayout layout, SlotKind kind) => layout.Slots.Count(slot => slot.Kind == kind);
    }
}
