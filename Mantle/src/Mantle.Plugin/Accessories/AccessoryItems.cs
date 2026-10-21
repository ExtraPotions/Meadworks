// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Accessories
{
    /// <summary>The registry (<see cref="AccessoryRegistry"/>) asked about the game's items, and the slot kinds the two slots are.</summary>
    public static class AccessoryItems
    {
        public static SlotKind KindOf(AccessorySlot slot) => slot == AccessorySlot.Ring ? SlotKind.Ring : SlotKind.Belt;

        /// <summary>The accessory slot kind an item is tagged for by the registry, or null for an ordinary item. Independent of any layout.</summary>
        public static SlotKind? KindOf(ItemDrop.ItemData item)
        {
            if (item == null)
                return null;
            AccessorySlot? slot = AccessoryRegistry.Current.SlotOf(ItemNames.PrefabName(item));
            return slot.HasValue ? KindOf(slot.Value) : (SlotKind?)null;
        }
    }
}
