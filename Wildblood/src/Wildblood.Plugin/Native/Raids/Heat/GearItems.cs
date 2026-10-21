// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Which of the game's items mark a gear tier (features/raids.md section 3), by prefab name, turned into the name
    /// tokens ("$item_bronze") that known materials and worn items carry. The names were checked against the game's own
    /// item list (the workshop's measured codex, Reference/Codex/data/items.json, 2026-10-10); a name the game does not
    /// have is simply left out.
    /// <list type="table">
    /// <item>0 - leather, rags, troll hide, bone, deer: no metal (the default, listed nowhere)</item>
    /// <item>1 - metal Bronze; armour bronze, and the bear's (Berserker) set of the Black Forest</item>
    /// <item>2 - metal Iron; armour iron and root (the Swamp)</item>
    /// <item>3 - metal Silver; armour wolf with the drake helmet, and fenris (the Mountain)</item>
    /// <item>4 - metal Black metal; armour padded, lox and vilebone (the Plains)</item>
    /// <item>5 - Mistlands: carapace and refined eitr held; armour carapace and eitr-weave (Mage)</item>
    /// <item>6 - Flametal (new and old) held; armour flametal, Ask, Embla (Ashlands Mage)</item>
    /// <item>7 - the Deep North: Bloodgold (<c>Gold</c>) held, or one of its three sets worn (names checked against the
    /// Valheim 1.0.20 item prefabs, 2026-10-19). Hel's Trial starts there (Arena/Trial/ArenaPower), and raids weigh it
    /// against its own fair stake (<see cref="RaidTable.FairStake"/>)</item>
    /// </list>
    /// Built from ObjectDB the first time it is asked, and again when the game builds a new ObjectDB (a new world).
    /// </summary>
    internal static class GearItems
    {
        private static readonly string[][] Metals =
        {
            new string[0],
            new[] { "Bronze" },
            new[] { "Iron" },
            new[] { "Silver" },
            new[] { "BlackMetal" },
            new[] { "Carapace", "Eitr" },
            new[] { "FlametalNew", "Flametal" },
            new[] { "Gold" },
        };

        private static readonly string[][] Armour =
        {
            new string[0],
            new[] { "HelmetBronze", "ArmorBronzeChest", "ArmorBronzeLegs", "HelmetBerserkerHood", "ArmorBerserkerChest",
                "ArmorBerserkerLegs" },
            new[] { "HelmetIron", "ArmorIronChest", "ArmorIronLegs", "HelmetRoot", "HelmetRootCrown", "ArmorRootChest",
                "ArmorRootLegs" },
            new[] { "HelmetDrake", "ArmorWolfChest", "ArmorWolfLegs", "HelmetFenring", "ArmorFenringChest",
                "ArmorFenringLegs" },
            new[] { "HelmetPadded", "ArmorPaddedCuirass", "ArmorPaddedGreaves", "HelmetLox", "ArmorLoxChest", "ArmorLoxLegs",
                "HelmetBerserkerUndead", "ArmorBerserkerUndeadChest", "ArmorBerserkerUndeadLegs" },
            new[] { "HelmetCarapace", "ArmorCarapaceChest", "ArmorCarapaceLegs", "HelmetMage", "ArmorMageChest",
                "ArmorMageLegs" },
            new[] { "HelmetFlametal", "ArmorFlametalChest", "ArmorFlametalLegs", "HelmetAshlandsMediumHood",
                "ArmorAshlandsMediumChest", "ArmorAshlandsMediumlegs", "HelmetMage_Ashlands", "ArmorMageChest_Ashlands",
                "ArmorMageLegs_Ashlands" },
            new[] { "HelmetDNHeavy", "ArmorDeepNorthHeavyChest", "ArmorDeepNorthHeavylegs", "HelmetDNMediumHood",
                "ArmorDeepNorthMediumChest", "ArmorDeepNorthMediumlegs", "HelmetDNMage", "ArmorDeepNorthMageChest",
                "ArmorDeepNorthMagelegs" },
        };

        private static readonly Dictionary<string, int> MetalTiers = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> ArmourTiers = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> Empty = new Dictionary<string, int>();
        private static ObjectDB? _builtFor;

        /// <summary>Every metal token with its tier, for a pass over a player's known materials.</summary>
        public static IReadOnlyDictionary<string, int> MetalTokens => Ready() ? MetalTiers : Empty;

        /// <summary>The tier a worn item marks; 0 for anything not listed, or before the item list is up.</summary>
        public static int ArmourTier(ItemDrop.ItemData? item) =>
            item?.m_shared != null && Ready() && ArmourTiers.TryGetValue(item.m_shared.m_name, out int tier) ? tier : 0;

        private static bool Ready()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0)
            {
                return false;
            }
            if (!ReferenceEquals(db, _builtFor))
            {
                _builtFor = db;
                Fill(db, Metals, MetalTiers);
                Fill(db, Armour, ArmourTiers);
            }
            return true;
        }

        private static void Fill(ObjectDB db, string[][] names, Dictionary<string, int> into)
        {
            into.Clear();
            for (int tier = 1; tier < names.Length; tier++)
            {
                foreach (string name in names[tier])
                {
                    GameObject? prefab = db.GetItemPrefab(name);
                    ItemDrop? drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                    if (drop != null && drop.m_itemData?.m_shared != null)
                    {
                        into[drop.m_itemData.m_shared.m_name] = tier;
                    }
                }
            }
        }
    }
}
