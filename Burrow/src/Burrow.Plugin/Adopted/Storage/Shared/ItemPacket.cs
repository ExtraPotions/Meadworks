using UnityEngine;

namespace Burrow.Shared
{
    /// <summary>
    /// One item on the wire: the prefab name, then the game's own per-item field list as
    /// <c>ItemDrop.ItemData.Save</c> writes it into a chest's ZDO (durability, grid position, world level, the
    /// flag byte, quality, stack, variant, crafter id and name, prefab hash, custom data, cheated flag). The
    /// reader rebuilds the item from the prefab, as <c>Inventory.Load</c> does, so its shared data is the live one
    /// and quality, variant, durability, crafter and custom data survive. An item without a prefab cannot travel:
    /// an empty name is written and the reader returns null.
    /// </summary>
    public static class ItemPacket
    {
        public static bool Write(ZPackage pkg, ItemDrop.ItemData item)
        {
            string prefab = item != null && item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            pkg.Write(prefab);
            if (prefab.Length == 0)
                return false;
            item.Save(pkg);
            return true;
        }

        public static ItemDrop.ItemData Read(ZPackage pkg)
        {
            string prefab = pkg.ReadString();
            if (prefab.Length == 0)
                return null;
            GameObject prefabObject = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop drop = prefabObject != null ? prefabObject.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                Plugin.Log.LogWarning($"Burrow: received an item of unknown prefab {prefab}");
                return null;
            }
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            ItemDrop.ItemData.Load(pkg, item, Version.Item.ChunksNCheats);
            item.m_dropPrefab = prefabObject;
            item.m_equipped = false;
            if (item.m_stack < 1)
                item.m_stack = 1;
            return item;
        }

        /// <summary>A copy of a stack holding exactly <paramref name="amount"/> units (at most the stack), unequipped.</summary>
        public static ItemDrop.ItemData Copy(ItemDrop.ItemData item, int amount)
        {
            ItemDrop.ItemData copy = item.Clone();
            copy.m_stack = Mathf.Clamp(amount, 1, Mathf.Max(1, item.m_stack));
            copy.m_equipped = false;
            return copy;
        }
    }
}
