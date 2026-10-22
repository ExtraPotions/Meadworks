// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Barter.Availability;
using ExtraPotions.Barter.Lists;
using UnityEngine;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>What Barter.Core asks the running game: world keys, the local player's keys and known items, and item stack sizes.</summary>
    internal sealed class TradeWorld : ITradeContext
    {
        internal static readonly TradeWorld Instance = new TradeWorld();

        public bool HasGlobalKey(string key) => ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);

        public bool HasPlayerKey(string key) => Player.m_localPlayer != null && Player.m_localPlayer.HaveUniqueKey(key);

        public bool IsKnown(string prefab)
        {
            ItemDrop drop = Item(prefab);
            return drop != null && Player.m_localPlayer != null && Player.m_localPlayer.IsMaterialKnown(drop.m_itemData.m_shared.m_name);
        }

        public int? MaxStack(string prefab)
        {
            ItemDrop drop = Item(prefab);
            return drop != null ? drop.m_itemData.m_shared.m_maxStackSize : (int?)null;
        }

        /// <summary>The item with this prefab name, or null (also before the item database exists).</summary>
        internal static ItemDrop Item(string prefab)
        {
            if (string.IsNullOrWhiteSpace(prefab) || ObjectDB.instance == null)
                return null;
            GameObject found = ObjectDB.instance.GetItemPrefab(prefab.Trim());
            return found != null ? found.GetComponent<ItemDrop>() : null;
        }

        /// <summary>The trader's name as the lists key it.</summary>
        internal static string TraderName(Trader trader) =>
            trader == null ? string.Empty : TraderNames.Normalize(Utils.GetPrefabName(trader.gameObject));

        /// <summary>The prefab name of a held item, empty when it has none.</summary>
        internal static string PrefabName(ItemDrop.ItemData item) =>
            item != null && item.m_dropPrefab != null ? Utils.GetPrefabName(item.m_dropPrefab) : string.Empty;
    }
}
