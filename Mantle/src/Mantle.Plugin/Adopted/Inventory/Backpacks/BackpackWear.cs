using Mantle.Core;
using Mantle.Elite;
using Mantle.Layout;

namespace Mantle.Backpacks
{
    /// <summary>
    /// The local player's frame (from <see cref="PlayerTick"/>). When a backpack went into or out of its slot, or its
    /// slots changed (the YAML, a setting, its EliteCrafting Deep Pockets), the layout is applied again: slots added at the
    /// bottom, or taken away with what they held moved to free cells and the rest dropped at the player's feet (the user's
    /// choice). The pack in the slot carries the equipped flag (<see cref="BackpackEquip.Sync"/>), and EliteCrafting hears
    /// when the worn pack changes (<see cref="WornPackLink"/>). The player's ZDO names the worn pack, so every client
    /// shows it on the back. Only here, in Update: Keep On Death (Backpack) takes the pack out of the inventory for the length
    /// of <c>CreateTombStone</c>, which no frame sees, and a dead player is left alone.
    /// </summary>
    public static class BackpackWear
    {
        public static void Tick(Player player)
        {
            if (player.IsDead())
                return;
            BackpackEquip.Sync(player);
            ItemDrop.ItemData pack = Backpack.WornItem(player);
            WornPackLink.Follow(player, pack);
            if (InventoryState.Active && InventoryState.Layout.BackpackSlots != BackpackSettings.SlotsOf(pack))
                LayoutApply.Apply(player, dropOverflow: true);
            Show(player, BackpackCatalog.Of(pack));
        }

        /// <summary>
        /// Written only when it changes, so the change reaches every client once. With Show Worn Backpack off the pack is
        /// named as none: only the model reads this key (<see cref="BackpackMount"/>), so every client hangs nothing while
        /// the pack still gives its slots and carry weight.
        /// </summary>
        private static void Show(Player player, BackpackKind worn)
        {
            ZDO zdo = player.m_nview != null && player.m_nview.IsOwner() ? player.m_nview.GetZDO() : null;
            int hash = worn != null && BackpackSettings.ShowWorn.Value ? worn.Hash : 0;
            if (zdo != null && zdo.GetInt(Backpack.WornKey) != hash)
                zdo.Set(Backpack.WornKey, hash);
        }
    }
}
