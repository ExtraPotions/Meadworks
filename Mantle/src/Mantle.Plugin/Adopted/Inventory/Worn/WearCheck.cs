using System.Collections.Generic;

using ItemType = ItemDrop.ItemData.ItemType;

namespace Mantle.Worn
{
    /// <summary>
    /// Whether Auto Equip may put a piece on now (<see cref="GearKeep"/>, <see cref="ChestWear"/>): the guards of the game's
    /// EquipItem, checked first so a refused equip changes nothing and shows nothing every frame. Not attacking, dodging
    /// or swimming, not broken; the DLC and, for utilities and trinkets, the world level, whose messages the game shows
    /// when <c>tell</c> is set (a right click), never from the frame's check.
    /// </summary>
    public static class WearCheck
    {
        public static bool Guards(Humanoid humanoid, ItemDrop.ItemData item, bool tell)
        {
            if (humanoid.InAttack() || humanoid.InDodge() || (humanoid.IsSwimming() && !humanoid.IsOnGround()))
                return false;
            if (item.m_shared.m_useDurability && item.m_durability <= 0f)
                return false;
            if (item.m_shared.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(item.m_shared.m_dlc))
                return Refuse(humanoid, tell, "$msg_dlcrequired");
            bool levelled = item.m_shared.m_itemType == ItemType.Utility || item.m_shared.m_itemType == ItemType.Trinket;
            if (levelled && Game.m_worldLevel > 0 && item.m_worldLevel < Game.m_worldLevel)
                return Refuse(humanoid, tell, "$msg_ng_item_too_low");
            return true;
        }

        /// <summary>
        /// A utility is worn beside the others (<see cref="ExtraUtilities"/>) or as the game's one while that is empty;
        /// otherwise putting it on would take another worn utility off, which the frame's check never does.
        /// </summary>
        public static bool Beside(Humanoid humanoid, ItemDrop.ItemData item) =>
            item.m_shared.m_itemType != ItemType.Utility || humanoid.m_utilityItem == null || ExtraUtilities.Takes(humanoid, item);

        /// <summary>The cell of a worn utility of the same name in the cells, else of the game's utility; (-1, -1) for none.</summary>
        public static Vector2i Replaced(Humanoid humanoid, ItemDrop.ItemData item, IReadOnlyList<Vector2i> cells)
        {
            Inventory inventory = humanoid.GetInventory();
            Vector2i game = new Vector2i(-1, -1);
            for (int i = 0; i < cells.Count; i++)
            {
                ItemDrop.ItemData there = inventory.GetItemAt(cells[i].x, cells[i].y);
                if (there == null || !humanoid.IsItemEquiped(there))
                    continue;
                if (there.m_shared.m_name == item.m_shared.m_name)
                    return cells[i];
                if (there == humanoid.m_utilityItem)
                    game = cells[i];
            }
            return game;
        }

        private static bool Refuse(Humanoid humanoid, bool tell, string message)
        {
            if (tell)
                humanoid.Message(MessageHud.MessageType.Center, message);
            return false;
        }
    }
}
