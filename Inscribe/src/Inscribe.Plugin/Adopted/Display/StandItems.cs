// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// The item hanging on an item stand or an armor stand slot, read back from the stand's ZDO (the game keeps only
    /// the prefab hash and the saved item bytes there: <c>ItemDrop.SaveToZDO</c>, <c>"itemData"</c> or
    /// <c>"&lt;slot&gt;_itemData"</c>). Decoded into a private copy of the prefab's item data only when the ZDO's data
    /// revision or the item hash changes, so the per-frame hover costs one table lookup and three compares. The copy is
    /// never written anywhere and never enters an inventory. Viewing client only; a remote change arrives with the
    /// ZDO like any other, so every client reads the same data.
    /// </summary>
    internal static class StandItems
    {
        private sealed class Slot
        {
            public ZDO? Zdo;
            public uint Revision;
            public int Hash;
            public ItemDrop.ItemData? Item;
        }

        // Keyed on the stand (item stand) or on the slot object (armor stand): both live as long as the stand does.
        private static readonly ConditionalWeakTable<object, Slot> Table = new ConditionalWeakTable<object, Slot>();

        /// <summary>The item stored under <paramref name="index"/> (-1: an item stand), or null when none or unknown.</summary>
        public static ItemDrop.ItemData? Read(object owner, ZDO zdo, int itemHash, int index)
        {
            if (itemHash == 0)
            {
                return null;
            }
            Slot slot = Table.GetOrCreateValue(owner);
            if (!ReferenceEquals(slot.Zdo, zdo) || slot.Revision != zdo.DataRevision || slot.Hash != itemHash)
            {
                slot.Zdo = zdo;
                slot.Revision = zdo.DataRevision;
                slot.Hash = itemHash;
                slot.Item = Decode(zdo, itemHash, index);
            }
            return slot.Item;
        }

        private static ItemDrop.ItemData? Decode(ZDO zdo, int itemHash, int index)
        {
            GameObject? prefab = ObjectDB.instance == null ? null : ObjectDB.instance.GetItemPrefab(itemHash);
            ItemDrop? drop = prefab == null ? null : prefab.GetComponent<ItemDrop>();
            if (drop == null)
            {
                return null;
            }
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab;          // the slot map and the tier ceiling read the prefab name
            ItemDrop.LoadFromZDO(item, zdo, index);
            return item;
        }
    }
}
