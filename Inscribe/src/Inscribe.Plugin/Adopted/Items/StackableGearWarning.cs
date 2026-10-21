// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// The load warning of rarity.md section 2 (DECISIONS.md RAR-6): every item of a class that rolls but stacks
    /// (another mod raised its max stack size) is named in the log once per prefab. Stack sizes are not forced back;
    /// such an item is simply no magic base, and the item-state writer refuses it. Scanned when an object database is
    /// set up (every peer) and again when the local player first spawns in it (a client), after other mods' synced
    /// settings apply; a respawn rescans only when the database, the rules or the item classes changed.
    /// </summary>
    [HarmonyPatch]
    internal static class StackableGearWarning
    {
        private static readonly HashSet<string> Warned = new HashSet<string>(System.StringComparer.Ordinal);
        private static ObjectDB? _spawnDb;
        private static int _spawnGeneration = -1;
        private static int _spawnClasses = -1;

        public static void Scan(ObjectDB? db)
        {
            if (db?.m_items == null)
            {
                return;
            }
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop? drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null && IsStackableGear(drop.m_itemData) && Warned.Add(prefab!.name))
                {
                    Log.Warn($"{prefab.name} ({drop.m_itemData.m_shared.m_name}) is gear but stacks to "
                        + $"{drop.m_itemData.m_shared.m_maxStackSize}: it cannot become magic, and magic copies of it "
                        + "can lose their inscriptions when the game merges them into a stack");
                }
            }
        }

        private static bool IsStackableGear(ItemDrop.ItemData item) =>
            item.m_shared != null && item.m_shared.m_maxStackSize > 1
            && ItemClasses.Classify(item).Rolls && !ItemClasses.IsStone(item);

        // Local player's client only.
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        private static void PlayerSpawned(Player __instance)
        {
            ObjectDB db = ObjectDB.instance;
            if (__instance != Player.m_localPlayer || db == null || !SpawnKeyChanged(db))
            {
                return;
            }
            Scan(db);
        }

        /// <summary>Remembers the (database, rules generation, class version) of this spawn; false when it is the last one's.</summary>
        private static bool SpawnKeyChanged(ObjectDB db)
        {
            int generation = ActiveRules.Generation;
            int classes = ItemClasses.Version;
            if (ReferenceEquals(db, _spawnDb) && generation == _spawnGeneration && classes == _spawnClasses)
            {
                return false;
            }
            _spawnDb = db;
            _spawnGeneration = generation;
            _spawnClasses = classes;
            return true;
        }
    }
}
