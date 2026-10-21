// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/LootChests.cs ItemCount). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: takes the saved byte array instead of a ZDO, so it runs in the portable tests.
using System.IO;

namespace ExtraPotions.Delve.Chests
{
    /// <summary>What a loot chest holds, read from its saved inventory without resolving any item prefab.</summary>
    public static class ChestInventory
    {
        /// <summary>
        /// Item count straight from the saved inventory header: an int version, then a ushort count (version 108 and
        /// later) or an int count (older). Null means the chest has never been loaded, so it has no items array at all.
        /// </summary>
        public static int? ItemCount(byte[]? items)
        {
            if (items == null || items.Length < 4) return null;
            using (var reader = new BinaryReader(new MemoryStream(items)))
            {
                int version = reader.ReadInt32();
                if (version >= 108) return items.Length >= 6 ? reader.ReadUInt16() : (int?)null;
                return items.Length >= 8 ? reader.ReadInt32() : (int?)null;
            }
        }
    }
}
