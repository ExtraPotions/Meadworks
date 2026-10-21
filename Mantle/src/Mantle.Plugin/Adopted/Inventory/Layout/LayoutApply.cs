using System.Collections.Generic;
using Mantle.Core;
using Mantle.Panels;
using Mantle.Worn;

namespace Mantle.Layout
{
    /// <summary>
    /// Puts the local player's inventory into the layout the settings ask for: moves the items (<see cref="LayoutMigration"/>),
    /// sizes the inventory, records the layout in the character's data, puts worn items into their slots and sizes the
    /// panel. Runs when the character loads (without dropping anything: the player is not in the world yet), when the
    /// game sets the inventory size at spawn or after a trader row (dropping what does not fit, as the game does), and
    /// when a layout setting or FeastMaster's food count changes. With the module off and no record nothing is done,
    /// so another inventory mod keeps its rows; with the module off and a record, the items come back into the game's
    /// grid once and the record goes.
    /// </summary>
    public static class LayoutApply
    {
        /// <summary>Wide enough that the game's load keeps every item: it refuses a column beyond the width.</summary>
        private const int LoadWidth = 32;
        private static int applyingDepth;
        internal static bool IsApplying => applyingDepth != 0;

        public static bool Wanted(Player player) => Plugin.WorldReady && (InventorySettings.Enabled.Value || LayoutRecord.Has(player));

        /// <summary>
        /// Player.Load prefix: nothing worn yet, every saved column accepted, worn slots left alone until the layout is
        /// applied (the load patch's finalizer resumes them). The layout set here is provisional, so the game's own
        /// equip of the saved items already wears every utility in the slots.
        /// </summary>
        public static void BeforeLoad(Player player)
        {
            if (!Plugin.WorldReady) return;
            ExtraUtilities.Reset();
            BiomeLordsLink.Reset();
            InventoryState.Set(player, LayoutBuilder.Wanted(player));
            player.GetInventory().m_width = LoadWidth;
            WornPlacement.Suspend();
        }

        /// <summary>Player.Load postfix: the record and <c>invrows</c> are read now; nothing drops before the spawn.</summary>
        public static void AfterLoad(Player player)
        {
            if (Wanted(player))
            {
                Apply(player, dropOverflow: false);
                return;
            }
            player.GetInventory().m_width = InventorySettings.GameWidth;
            InventoryState.Set(player, null);
            GridContract.Clear(player);
        }

        public static void Apply(Player player, bool dropOverflow)
        {
            if (!Plugin.WorldReady) return;
            applyingDepth++;
            try
            {
                Inventory inventory = player.GetInventory();
                InventoryLayout wanted = LayoutBuilder.Wanted(player);
                LayoutMigration plan = LayoutMigration.Plan(new List<ItemDrop.ItemData>(inventory.GetAllItems()), LayoutRecord.Read(player), wanted);
                plan.Apply();
                inventory.m_width = wanted.Width;
                inventory.SetHeight(wanted.Height);
                InventoryState.Set(player, wanted);
                Remember(player, wanted);
                int dropped = dropOverflow ? Drop(player, plan.Overflow) : 0;
                Settle(player);
                inventory.Changed();
                if (InventoryGui.instance != null)
                    InventoryGui.instance.SetInventorySize(wanted.MainRows);
                SlotElements.Invalidate();   // a backpack's blocked cells may change with the element count the same
                Plugin.Log.LogInfo($"inventory laid out {wanted.Width} x {wanted.MainRows} with {wanted.Slots.Count} slots: "
                    + $"{plan.Moved} items moved, {dropped} dropped, {(dropOverflow ? 0 : plan.Overflow.Count)} waiting for the spawn");
            }
            finally { applyingDepth--; }
        }

        /// <summary>The record for the next layout, and the main grid for other mods (<see cref="GridContract"/>).</summary>
        private static void Remember(Player player, InventoryLayout layout)
        {
            if (InventorySettings.Enabled.Value)
            {
                LayoutRecord.Write(player, layout);
                GridContract.Write(player, layout);
                return;
            }
            LayoutRecord.Clear(player);
            GridContract.Clear(player);
        }

        private static void Settle(Player player)
        {
            if (InventorySettings.Enabled.Value)
                WornPlacement.SettleAll(player);
            else
                ExtraUtilities.TakeOffAll(player);
        }

        /// <summary>What found no cell goes to the ground through the game's own drop, with one message (into BiomeLords' crate when it took its rows away).</summary>
        private static int Drop(Player player, List<ItemDrop.ItemData> overflow)
        {
            if (BiomeLordsLink.Spill(player, overflow))
                return overflow.Count;
            foreach (ItemDrop.ItemData item in overflow)
                player.DropItem(player.GetInventory(), item, item.m_stack);
            if (overflow.Count > 0)
                player.Message(MessageHud.MessageType.Center, string.Format(Language.Localize(Words.Dropped), overflow.Count));
            return overflow.Count;
        }
    }
}
