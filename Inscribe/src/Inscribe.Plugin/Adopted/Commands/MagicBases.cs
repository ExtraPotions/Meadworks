// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Loot;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// Item prefabs for <c>inscribe roll</c>: a named prefab (exact, then case-insensitive), or a random magic base of an
    /// item class (classes-and-tiers.md section 9). The random pick is from the gear drop pool (<see cref="GearPool.Bases"/>,
    /// drops.md section 8), so a class roll samples what could drop; a class with no drop-eligible base falls back to
    /// every magic base of the class. Reads the local object database; command path only.
    /// </summary>
    internal static class MagicBases
    {
        public static GameObject? Resolve(string arg, System.Random random, out string? problem)
        {
            ItemClass? itemClass = ItemClasses.Get(arg.ToLowerInvariant());
            if (itemClass != null)
            {
                GameObject? picked = RandomOfClass(itemClass.Id, random);
                problem = picked == null ? $"no magic base of class {itemClass.Id} in the object database." : null;
                return picked;
            }
            GameObject? prefab = arg.Length == 0 ? null : FindPrefab(arg);
            problem = arg.Length == 0 ? "which prefab or class?"
                : prefab == null ? $"'{arg}' is neither an item prefab nor an item class ({ClassIds()})."
                : !ItemClasses.IsMagicBase(prefab.GetComponent<ItemDrop>().m_itemData) ? $"{prefab.name} is not a magic base (stackable, a class that never rolls, or a rune)."
                : null;
            return problem == null ? prefab : null;
        }

        private static GameObject? FindPrefab(string name)
        {
            ObjectDB? db = ObjectDB.instance;
            if (db == null)
            {
                return null;
            }
            GameObject? exact = db.GetItemPrefab(name);
            if (exact != null && exact.GetComponent<ItemDrop>() != null)
            {
                return exact;
            }
            return db.m_items.Find(go => go != null && go.GetComponent<ItemDrop>() != null
                && string.Equals(go.name, name, StringComparison.OrdinalIgnoreCase));
        }

        // The drop pool's bases of the class (Loot's GearPool: same exclusions as a drop), else any magic base of it.
        private static GameObject? RandomOfClass(string classId, System.Random random)
        {
            List<GameObject> pool = new List<GameObject>();
            foreach (GearBase gear in GearPool.Bases)
            {
                if (gear.Class.ClassId == classId)
                {
                    pool.Add(gear.Prefab);
                }
            }
            if (pool.Count == 0)
            {
                AnyOfClass(classId, pool);
            }
            return pool.Count == 0 ? null : pool[random.Next(pool.Count)];
        }

        private static void AnyOfClass(string classId, List<GameObject> into)
        {
            foreach (GameObject go in ObjectDB.instance?.m_items ?? new List<GameObject>())
            {
                ItemDrop? drop = go != null ? go.GetComponent<ItemDrop>() : null;
                if (drop != null && ItemClasses.IsMagicBase(drop.m_itemData) && ItemClasses.Classify(drop.m_itemData).ClassId == classId)
                {
                    into.Add(go!);
                }
            }
        }

        private static string ClassIds()
        {
            List<string> ids = new List<string>();
            foreach (ItemClass itemClass in ItemClasses.All)
            {
                if (itemClass.Rolls)
                {
                    ids.Add(itemClass.Id);
                }
            }
            return string.Join(", ", ids);
        }
    }
}
