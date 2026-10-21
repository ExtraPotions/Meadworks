namespace Mantle.Panels
{
    /// <summary>
    /// What the player holds, for the stat sheet's weapon and shield lines (<see cref="HeldStatLines"/>). The right
    /// hand's item and the left's, each the sheathed one while the hands are hidden: the game unequips both while
    /// sheathed and keeps them as <c>m_hiddenRightItem</c> and <c>m_hiddenLeftItem</c> to draw again. The blocker is the
    /// game's <c>GetCurrentBlocker</c>: the left hand's item (a shield, a bow, a torch), else the weapon in the right.
    /// The ammo is what <c>Attack.UseAmmo</c> would take: the equipped ammo while it is carried and fits the weapon,
    /// else the inventory's own search (Mantle's tacklebox and Ammo slots first, <c>Slots/AmmoSearch</c>).
    /// </summary>
    public static class HeldItems
    {
        public static ItemDrop.ItemData Right(Player player) => Held(player, player.m_rightItem, player.m_hiddenRightItem);

        public static ItemDrop.ItemData Left(Player player) => Held(player, player.m_leftItem, player.m_hiddenLeftItem);

        private static ItemDrop.ItemData Held(Player player, ItemDrop.ItemData shown, ItemDrop.ItemData hidden)
        {
            if (shown != null)
                return shown;
            return hidden != null && player.GetInventory().ContainsItem(hidden) ? hidden : null;
        }

        public static ItemDrop.ItemData Blocker(Player player)
        {
            ItemDrop.ItemData left = Left(player);
            if (left != null)
                return left;
            ItemDrop.ItemData right = Right(player);
            return right != null && right.IsWeapon() ? right : null;
        }

        /// <summary>The ammo a shot of <paramref name="weapon"/> would use, or null when it takes none or none is carried.</summary>
        public static ItemDrop.ItemData Ammo(Player player, ItemDrop.ItemData weapon)
        {
            string type = weapon.m_shared.m_ammoType;
            if (string.IsNullOrEmpty(type))
                return null;
            Inventory inventory = player.GetInventory();
            ItemDrop.ItemData equipped = player.GetAmmoItem();
            if (equipped != null && inventory.ContainsItem(equipped) && equipped.m_shared.m_ammoType == type)
                return equipped;
            return inventory.GetAmmoItem(type);
        }
    }
}
