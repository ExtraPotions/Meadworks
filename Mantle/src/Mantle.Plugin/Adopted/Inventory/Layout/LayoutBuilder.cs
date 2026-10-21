using System.Collections.Generic;
using System;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Ring;
using Mantle.Tackle;
using Mantle.Slots;

namespace Mantle.Layout
{
    /// <summary>
    /// The layout the settings ask for. The main grid is Inventory Width wide and Inventory Rows tall plus the rows the
    /// player bought from the trader (the game's <c>invrows</c> key above its 4) and BiomeLords' Featherweight rows while
    /// that blessing is active (<see cref="BiomeLordsLink"/>); with no rows at all it is the two hand
    /// cells at the top left (<see cref="InventorySettings.HandCells"/>, hotbar keys 1 and 2). A worn backpack's slots
    /// follow those cells in reading order (<see cref="Backpack.SlotsFor"/>), filling the rest of a hands row first; the
    /// spare cells of a partly used last row stay blocked. The slots follow in a fixed order:
    /// Head, Chest, Legs, Feet (only while EliteEquipment wears its boots on their own, <see cref="EliteEquipmentLink.SeparateBoots"/>), Back, Backpack, Utility, Trinket, Ring, Belt, Food, Mead, Ammo, Purse, each group as many as <see cref="SlotCounts"/> says,
    /// then with Key Ring on one ring cell per key in Key Items (<see cref="KeyRing"/>), then with Tacklebox on the Tacklebox
    /// slot and as many cells as the box in it gives (<see cref="Tacklebox.CellsFor"/>), last because their count changes
    /// with the box, so no other slot moves when it does. With the module off the layout is
    /// the game's: 8 wide, <c>invrows</c> tall, no slots.
    /// </summary>
    public static class LayoutBuilder
    {
        public static InventoryLayout Wanted(Player player)
        {
            if (!InventorySettings.Enabled.Value)
                return GameLayout(player);
            int rows = InventorySettings.InventoryRows.Value + Math.Max(0, GameRows(player) - InventorySettings.GameRows)
                + BiomeLordsLink.BlessingRows(player);
            int width = InventorySettings.InventoryWidth.Value;
            int backpack = Backpack.SlotsFor(player);
            int baseCells = rows > 0 ? rows * width : InventorySettings.HandCells;
            return new InventoryLayout(width, (baseCells + backpack + width - 1) / width, Slots(player), backpack, baseCells);
        }

        /// <summary>The game's own layout: what a character without a record has, and what the module off gives.</summary>
        public static InventoryLayout GameLayout(Player player) => new InventoryLayout(InventorySettings.GameWidth, GameRows(player), new List<Slot>());

        /// <summary>The game's row count: its unique key <c>invrows</c>, 4 until set, clamped as the game clamps it.</summary>
        public static int GameRows(Player player)
        {
            if (player.TryGetUniqueKeyValue(Player.InventoryRowsKey, out string value) && int.TryParse(value, out int rows))
                return Math.Max(0, Math.Min(9, rows));
            return InventorySettings.GameRows;
        }

        private static List<Slot> Slots(Player player)
        {
            return SlotPlan.Build(new SlotPlanInput
            {
                Equipment = InventorySettings.EquipmentSlots.Value,
                Feet = InventorySettings.EquipmentSlots.Value && EliteEquipmentLink.SeparateBoots,
                Backpack = InventorySettings.BackpackSlot.Value,
                Utility = SlotCounts.Utility,
                Trinket = InventorySettings.TrinketSlot.Value,
                Rings = InventorySettings.RingSlots.Value,
                Belt = InventorySettings.BeltSlot.Value,
                Food = SlotCounts.Food,
                Mead = SlotCounts.Mead,
                Ammo = SlotCounts.Ammo,
                Purse = InventorySettings.CoinPurse.Value,
                Keys = KeyRing.On ? KeyRing.Prefabs.Count : 0,
                Tacklebox = TackleboxSettings.Active,
                TackleCells = TackleboxSettings.Active ? Tacklebox.CellsFor(player) : 0,
            });
        }
    }
}
