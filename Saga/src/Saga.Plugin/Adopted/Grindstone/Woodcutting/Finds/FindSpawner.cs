// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Finds/FindSpawner.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Drops a find's items on the tree's owner, the way the game drops a log's wood (TreeLog.Destroy): each drop is
    /// Object.Instantiate of the item prefab, which gives it a ZDO owned by this machine so every client sees it, then
    /// ItemDrop.OnCreateNew (the world level stamp). An amount is split into stacks of the item's
    /// m_shared.m_maxStackSize, each set with ItemDrop.SetStack (which clamps to that size and saves the stack to the
    /// ZDO). The drops start stacked 0.3 m apart like the game's own and get a small push, as CharacterDrop.DropItems
    /// gives creature loot, so they tumble out of the crown.
    /// </summary>
    public static class FindSpawner
    {
        /// <summary>The most drops one item of a find makes, whatever its amount and stack size.</summary>
        public const int MaxDrops = 50;

        private const float Scatter = 0.4f;
        private const float Step = 0.3f;
        private const float Tumble = 2f;

        /// <summary>Drops every item of the find around <paramref name="spot"/>; returns how many drops it made.</summary>
        public static int Spawn(FindEntry find, Vector3 spot, Vector3 away)
        {
            int made = 0;
            foreach (FindItem item in find.Items)
                made += SpawnItem(item, spot, away, made);
            return made;
        }

        private static int SpawnItem(FindItem item, Vector3 spot, Vector3 away, int index)
        {
            int amount = Random.Range(item.Min, item.Max + 1);
            ItemDrop prefab = amount > 0 ? FindPrefabs.Item(item.Prefab) : null;
            if (prefab == null)
                return 0;
            int maxStack = Mathf.Max(1, prefab.m_itemData.m_shared.m_maxStackSize);
            int made = 0;
            for (; amount > 0 && made < MaxDrops; made++)
            {
                int stack = Mathf.Min(amount, maxStack);
                Drop(prefab.gameObject, stack, spot + Vector3.up * (Step * (index + made)), away);
                amount -= stack;
            }
            return made;
        }

        private static void Drop(GameObject prefab, int stack, Vector3 position, Vector3 away)
        {
            Vector2 scatter = Random.insideUnitCircle * Scatter;
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject spawned = Object.Instantiate(prefab, position + new Vector3(scatter.x, 0f, scatter.y), rotation);
            ItemDrop drop = spawned.GetComponent<ItemDrop>();
            ItemDrop.OnCreateNew(drop);
            drop.SetStack(stack);
            Push(spawned.GetComponent<Rigidbody>(), away);
        }

        /// <summary>Away from the trunk and a little upwards, with some spin of direction.</summary>
        private static void Push(Rigidbody body, Vector3 away)
        {
            if (body == null)
                return;
            Vector3 jitter = Random.insideUnitSphere;
            jitter.y = Mathf.Abs(jitter.y);
            body.AddForce((away + jitter) * Tumble, ForceMode.VelocityChange);
        }
    }
}
