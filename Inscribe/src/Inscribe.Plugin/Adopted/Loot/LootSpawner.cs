// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Puts our drops into the world with the game's own small scatter and upward push (drops.md section 1). During a
    /// creature's death the items are held for its ragdoll instead and spawned here when it dissolves
    /// (<see cref="CorpseLoot"/>), as the game does with its own loot. Everything goes through the game's
    /// <c>ItemDrop.DropItem</c>, which saves the item's full data into the new ZDO before returning, so no peer can see a
    /// dropped item without its custom data (game notes Q15). Runs on the creature's owner; the game replicates the rest.
    /// </summary>
    internal static class LootSpawner
    {
        private const float DropArea = 0.5f;
        private const float Push = 5f;

        private static readonly Dictionary<StoneDef, int> Grouped = new Dictionary<StoneDef, int>();
        private static readonly HashSet<string> MissingPrefabs = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>Spawns <paramref name="amount"/> of the item near <paramref name="center"/>, or holds it for the ragdoll during a death.</summary>
        public static void Drop(ItemDrop.ItemData item, int amount, Vector3 center)
        {
            if (CorpseLoot.Hold(item, amount))
            {
                return;
            }
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0, 360), 0f);
            ItemDrop drop = ItemDrop.DropItem(item, amount, center + Random.insideUnitSphere * DropArea, rotation);
            Rigidbody body = drop.GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 direction = Random.insideUnitSphere;
                direction.y = Mathf.Abs(direction.y);
                body.AddForce(direction * Push, ForceMode.VelocityChange);
            }
        }

        /// <summary>Drops the planned stones, one world stack per stone kind (split at the stack size). Returns how many.</summary>
        public static int DropStones(List<StoneDef> stones, Vector3 center, bool cheated, bool creatureDrops = false)
        {
            Grouped.Clear();
            foreach (StoneDef stone in stones)
            {
                Grouped.TryGetValue(stone, out int count);
                Grouped[stone] = count + 1;
            }
            int dropped = 0;
            foreach (KeyValuePair<StoneDef, int> pair in Grouped)
            {
                dropped += DropStone(pair.Key, pair.Value, center, cheated, creatureDrops);
            }
            Grouped.Clear();
            return dropped;
        }

        private static int DropStone(StoneDef stone, int amount, Vector3 center, bool cheated, bool creatureDrops)
        {
            ItemDrop.ItemData? item = StoneItem(stone, cheated);
            if (item == null)
            {
                return 0;
            }
            if (creatureDrops) amount = Runtime.InscribeLootInterop.StackAmount(item, amount);
            int stack = Mathf.Max(1, item.m_shared.m_maxStackSize);
            for (int left = amount; left > 0; left -= stack)
            {
                Drop(item, Mathf.Min(left, stack), center);
            }
            return amount;
        }

        /// <summary>
        /// A fresh stack of one stone (amount 1), or null when its prefab is not registered. Stones carry no custom data;
        /// the world level keeps them stacking with other stones of this world. Also used to fill chests.
        /// </summary>
        internal static ItemDrop.ItemData? StoneItem(StoneDef stone, bool cheated)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(stone.Prefab) : null!;
            ItemDrop template = prefab != null ? prefab.GetComponent<ItemDrop>() : null!;
            if (template == null)
            {
                if (MissingPrefabs.Add(stone.Prefab))
                {
                    Log.Warn($"rune drops: prefab {stone.Prefab} of rune '{stone.Id}' is not registered; it cannot drop");
                }
                return null;
            }
            ItemDrop.ItemData item = template.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_worldLevel = Game.m_worldLevel;
            item.m_cheated = cheated;
            item.m_customData.Clear();
            return item;
        }
    }
}
