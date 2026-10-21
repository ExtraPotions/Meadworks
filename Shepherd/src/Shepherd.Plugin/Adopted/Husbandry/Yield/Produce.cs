// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Yield/Produce.cs, ProduceTable.cs and
// ProduceTables.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; NativeHusbandry exception guard; the clock, weighted pick and produce test in the
// portable YieldMath and ProduceClock; the next roll kept in milliseconds under shepherd.produce_next; Verbose logging.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Yield;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Produce: a fed tamed animal now and then drops one of its own materials without being killed, on the creature's
    /// ZDO owner. The game calls Tameable.TamingUpdate every 3 s on every machine that has the creature; a postfix acts
    /// only on the owner, for a tamed creature that is not hungry and whose kind produces something, while Husbandry is
    /// on and "Produce Chance At 100" is above 0. The first tick only starts the clock (so taming never pays out at
    /// once); once "Produce Interval" has passed, a tick with a keeper in range restarts it and rolls the best keeper's
    /// share of the chance. A success drops one item beside the animal, the way the game spawns drops: a networked
    /// instance owned by this machine, with the world's resource rate applied to its stack.
    /// </summary>
    public static class Produce
    {
        private const float Gap = 0.3f;
        private const float DropHeight = 0.5f;

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.TamingUpdate))]
        private static class Tick
        {
            [HarmonyPostfix]
            private static void Postfix(Tameable __instance)
            {
                if (HusbandrySkill.Active && HusbandryYieldSettings.ProduceChance.Value > 0f && IsFedOwnedTame(__instance))
                    NativeHusbandry.Run("produce", static tame => Advance(tame), __instance);
            }
        }

        private static bool IsFedOwnedTame(Tameable tameable) =>
            tameable.m_nview != null && tameable.m_nview.IsValid() && tameable.m_nview.IsOwner()
            && tameable.m_character != null && tameable.m_character.IsTamed() && !tameable.IsHungry();

        private static void Advance(Tameable tameable)
        {
            ProduceTable table = ProduceTables.For(tameable.m_character);
            if (table.IsEmpty)
                return;
            ZDO zdo = tameable.m_nview.GetZDO();
            long now = Herd.Now.Ticks / TimeSpan.TicksPerMillisecond;
            float chance = Keeper.Share(HusbandryYieldSettings.ProduceChance.Value, tameable.transform.position);
            ProduceTick tick = ProduceClock.Step(now, zdo.GetLong(BreedingKeys.ProduceNext, 0L), HusbandryYieldSettings.ProduceInterval.Value, chance);
            if (tick.Step == ProduceStep.Wait)
                return;
            zdo.Set(BreedingKeys.ProduceNext, tick.NextMs);
            if (tick.Step == ProduceStep.Roll && UnityEngine.Random.value < chance)
                Spawn(table.Pick(), tameable.m_character);
        }

        private static void Spawn(GameObject? prefab, Character animal)
        {
            if (prefab == null)
                return;
            Vector2 side = UnityEngine.Random.insideUnitCircle.normalized * (animal.GetRadius() + Gap);
            Vector3 position = animal.transform.position + new Vector3(side.x, DropHeight, side.y);
            GameObject spawned = UnityEngine.Object.Instantiate(prefab, position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            ItemDrop item = spawned.GetComponent<ItemDrop>();
            if (item == null)
                return;
            ItemDrop.OnCreateNew(item);
            item.SetStack(Game.instance.ScaleDrops(item.m_itemData, 1));
            NativeHusbandry.Verbose("produce: " + Herd.PrefabName(animal) + " gave " + prefab.name);
        }
    }

    /// <summary>
    /// One creature kind's produce: the item prefabs it can give while alive, each weighted by its drop chance in the
    /// creature's CharacterDrop. Read-only after it is built.
    /// </summary>
    public sealed class ProduceTable
    {
        private readonly List<GameObject> items = new List<GameObject>();
        private readonly List<double> weights = new List<double>();

        public bool IsEmpty => items.Count == 0;

        public IEnumerable<string> Names
        {
            get
            {
                foreach (GameObject item in items)
                    yield return item.name;
            }
        }

        public void Add(GameObject item, float weight)
        {
            if (item == null || weight <= 0f || items.Contains(item))
                return;
            items.Add(item);
            weights.Add(weight);
        }

        /// <summary>One item prefab, picked by weight; null when the table is empty.</summary>
        public GameObject? Pick()
        {
            int index = YieldMath.PickWeighted(weights.ToArray(), UnityEngine.Random.value);
            return index >= 0 ? items[index] : null;
        }
    }

    /// <summary>
    /// Which items each creature kind produces, discovered rather than listed and cached per prefab name: the items in
    /// the kind's own CharacterDrop that are not trophies, have no food value and are not cooking inputs
    /// (<see cref="YieldCatalog.IsCookingInput"/>), weighted by the drop's chance. Cleared whenever discovery runs.
    /// </summary>
    public static class ProduceTables
    {
        private static readonly Dictionary<string, ProduceTable> tables = new Dictionary<string, ProduceTable>(StringComparer.Ordinal);

        public static void Clear() => tables.Clear();

        public static ProduceTable For(Character creature)
        {
            string prefabName = Herd.PrefabName(creature);
            if (tables.TryGetValue(prefabName, out ProduceTable table))
                return table;
            table = Build(Drops(creature, prefabName));
            tables[prefabName] = table;
            if (!table.IsEmpty)
                NativeHusbandry.Verbose("Husbandry: " + prefabName + " produces " + string.Join(", ", table.Names) + ".");
            return table;
        }

        private static CharacterDrop Drops(Character creature, string prefabName)
        {
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            CharacterDrop? drops = prefab != null ? prefab.GetComponent<CharacterDrop>() : null;
            return drops != null ? drops : creature.GetComponent<CharacterDrop>();
        }

        private static ProduceTable Build(CharacterDrop drops)
        {
            var table = new ProduceTable();
            if (drops == null || drops.m_drops == null)
                return table;
            foreach (CharacterDrop.Drop drop in drops.m_drops)
            {
                if (drop != null && IsProduce(drop.m_prefab))
                    table.Add(drop.m_prefab, drop.m_chance);
            }
            return table;
        }

        private static bool IsProduce(GameObject prefab)
        {
            ItemDrop? item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            ItemDrop.ItemData.SharedData? shared = item != null ? item.m_itemData?.m_shared : null;
            return shared != null && YieldMath.IsProduce(shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy,
                shared.m_food + shared.m_foodStamina + shared.m_foodEitr, YieldCatalog.IsCookingInput(shared));
        }
    }
}
