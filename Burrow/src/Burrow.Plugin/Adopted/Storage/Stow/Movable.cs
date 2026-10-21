namespace Burrow.Stow
{
    /// <summary>
    /// Whether an action may move, trash or route an item: equipped items and quest items never move, the content
    /// of a favourite slot of the player inventory never moves, favourite item names are skipped by every action
    /// that stores, trashes or salvages (top up refills them, which does not move them), and a stack with a put
    /// to a shared chest under way waits for its answer.
    /// </summary>
    public static class Movable
    {
        /// <summary>The word that explains why the item stays, "" for a silent refusal, or null when it may move.</summary>
        public static string Blocker(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null)
                return "";
            if (Burrow.Core.PackPanelGrid.InSlot(player, inventory, item)) return "";
            if (item.m_equipped || (player != null && player.IsItemEquiped(item)))
                return StowWords.EquippedKept;
            if (item.m_shared.m_questItem || StackMover.IsPending(item))
                return "";
            bool playerInventory = player != null && inventory == player.GetInventory();
            if (playerInventory && Favourites.IsFavouriteSlot(item.m_gridPos))
                return StowWords.FavouriteKept;
            if (Favourites.IsFavouriteItem(item))
                return StowWords.FavouriteKept;
            return null;
        }

        public static bool CanMove(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            return Blocker(player, inventory, item) == null;
        }
    }
}
