using System;

namespace Burrow.Shared
{
    /// <summary>Compares the complete saved identity without count, position or operational flags.</summary>
    internal static class ItemCompatibility
    {
        internal static bool Same(ItemDrop.ItemData left, ItemDrop.ItemData right)
        {
            string identity = Key(left);
            return identity != null && string.Equals(identity, Key(right), StringComparison.Ordinal);
        }

        internal static string Key(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null)
                return null;
            ItemDrop.ItemData copy = item.Clone();
            copy.m_stack = 1;
            copy.m_gridPos = new Vector2i(0, 0);
            copy.m_equipped = false;
            copy.m_pickedUp = true;
            ZPackage package = new ZPackage();
            return ItemPacket.Write(package, copy) ? Convert.ToBase64String(package.GetArray()) : null;
        }
    }
}
