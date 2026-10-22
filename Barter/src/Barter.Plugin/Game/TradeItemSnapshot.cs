// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtraPotions.Barter.Runtime
{
    internal static class TradeItemSnapshot
    {
        private const byte Version = 2;

        internal static string Encode(IReadOnlyList<SafeTradeInventory.Take> items)
        {
            if (items == null || items.Count == 0 || items.Count > 64) throw new InvalidDataException("Invalid item count");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version); writer.Write(items.Count);
                foreach (var take in items)
                {
                    var item = take.Item;
                    if (item == null || take.Amount <= 0 || take.Amount > item.m_stack || item.m_customData.Count > 256) throw new InvalidDataException("Invalid stack");
                    writer.Write(TradeWorld.PrefabName(item)); writer.Write(take.Amount); writer.Write(item.m_quality);
                    writer.Write(item.m_variant); writer.Write(item.m_durability); writer.Write(item.m_worldLevel);
                    writer.Write(item.m_equipped); writer.Write(item.m_pickedUp); writer.Write(item.m_cheated);
                    writer.Write(item.m_crafterID); writer.Write(item.m_crafterName ?? ""); writer.Write(item.m_customData.Count);
                    foreach (var pair in item.m_customData.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        if (pair.Key.Length > 1024 || pair.Value == null || pair.Value.Length > 16384) throw new InvalidDataException("Invalid item metadata");
                        writer.Write(pair.Key); writer.Write(pair.Value);
                    }
                    if (stream.Length > 24576) throw new InvalidDataException("Item metadata exceeds escrow bound");
                }
                string encoded = Convert.ToBase64String(stream.ToArray());
                if (encoded.Length > 32768) throw new InvalidDataException("Item metadata exceeds escrow bound");
                return encoded;
            }
        }

        internal static IReadOnlyList<SafeTradeInventory.Take> Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length > 32768) throw new InvalidDataException("Invalid escrow snapshot");
            var items = new List<SafeTradeInventory.Take>();
            using (var stream = new MemoryStream(Convert.FromBase64String(encoded)))
            using (var reader = new BinaryReader(stream))
            {
                byte version = reader.ReadByte();
                if (version == 1)
                {
                    // Existing snapshots have an Int32 version header and an Int32 world level.
                    if (reader.ReadByte() != 0 || reader.ReadByte() != 0 || reader.ReadByte() != 0)
                        throw new InvalidDataException("Unsupported snapshot version");
                }
                else if (version != Version) throw new InvalidDataException("Unsupported snapshot version");
                int count = reader.ReadInt32(); if (count <= 0 || count > 64) throw new InvalidDataException("Invalid item count");
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadString(); if (name.Length == 0 || name.Length > 128) throw new InvalidDataException("Invalid prefab");
                    var prefab = TradeWorld.Item(name); if (prefab == null) throw new InvalidDataException("Missing item prefab");
                    var item = prefab.m_itemData.Clone(); item.m_dropPrefab = prefab.gameObject;
                    item.m_stack = reader.ReadInt32(); item.m_quality = reader.ReadInt32(); item.m_variant = reader.ReadInt32();
                    item.m_durability = reader.ReadSingle(); item.m_worldLevel = reader.ReadInt32();
                    item.m_equipped = reader.ReadBoolean(); item.m_pickedUp = reader.ReadBoolean(); item.m_cheated = reader.ReadBoolean();
                    item.m_crafterID = reader.ReadInt64(); item.m_crafterName = reader.ReadString();
                    int metadata = reader.ReadInt32();
                    if (metadata < 0 || metadata > 256 || item.m_crafterName.Length > 1024 || item.m_variant < 0 || item.m_stack <= 0 ||
                        item.m_stack > item.m_shared.m_maxStackSize || item.m_quality <= 0 || item.m_quality > item.m_shared.m_maxQuality ||
                        float.IsNaN(item.m_durability) || float.IsInfinity(item.m_durability) || item.m_durability < 0) throw new InvalidDataException("Invalid item state");
                    item.m_customData = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (int m = 0; m < metadata; m++)
                    {
                        string key = reader.ReadString(), value = reader.ReadString();
                        if (key.Length > 1024 || value.Length > 16384 || item.m_customData.ContainsKey(key)) throw new InvalidDataException("Invalid item metadata");
                        item.m_customData.Add(key, value);
                    }
                    items.Add(new SafeTradeInventory.Take(item, item.m_stack));
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Snapshot trailing bytes");
            }
            return items;
        }

        internal static bool Matches(TradeRequest request)
        {
            try
            {
                var items = Decode(request.Snapshot);
                return items.Sum(i => (long)i.Amount) == request.Amount && items.All(i =>
                    TradeWorld.PrefabName(i.Item) == request.Prefab && i.Item.m_quality == request.Quality);
            }
            catch (Exception) { return false; }
        }
    }
}
