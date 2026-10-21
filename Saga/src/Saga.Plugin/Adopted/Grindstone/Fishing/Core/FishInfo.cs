// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Core/FishInfo.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// What a fish is. A fish in the water is an item (ItemDrop with a Fish component), and its level is the item's
    /// quality: the game rolls 1 to 5 when it spawns (SpawnSystem, 20% per step), a big one grows on the hook
    /// (<see cref="BigOne"/>) and a legendary fish is 6 (<see cref="LegendarySpawn"/>). Quality already scales a fish's
    /// size (+40% per level), weight, pull and thrashes, and how many raw fish cleaning gives, so it is never used for
    /// Cooking stars; the fillets carry those.
    /// </summary>
    public static class FishInfo
    {
        /// <summary>The highest level the game rolls, and the highest a big one grows to.</summary>
        public const int MaxNaturalLevel = 5;

        /// <summary>A legendary fish's level (item quality).</summary>
        public const int LegendaryLevel = 6;

        /// <summary>A Perch's pull (Fish.m_staminaUse), the weakest in the game: the species scale's yardstick.</summary>
        private const float PerchPull = 3f;

        /// <summary>Each item prefab's Fish component (null for any other item), looked up once per prefab: tooltips ask every frame.</summary>
        private static readonly Dictionary<GameObject, Fish> fishOfPrefab = new Dictionary<GameObject, Fish>();

        public static ItemDrop Item(Fish fish) =>
            fish == null ? null : fish.m_itemDrop != null ? fish.m_itemDrop : fish.GetComponent<ItemDrop>();

        /// <summary>The fish's level, 1 when it has no item.</summary>
        public static int Level(Fish fish)
        {
            ItemDrop item = Item(fish);
            return item == null ? 1 : Mathf.Max(1, item.m_itemData.m_quality);
        }

        public static bool IsLegendary(int level) => level >= LegendaryLevel;

        /// <summary>The fish's prefab name ("Fish2"), the key of the angler's log.</summary>
        public static string Prefab(Fish fish) => fish == null ? "" : Utils.GetPrefabName(fish.gameObject);

        /// <summary>The fish's name in the player's language ("Pike").</summary>
        public static string Name(Fish fish) => fish == null ? "" : Localize(fish.m_name);

        /// <summary>
        /// How much a species is worth in experience: the square root of its pull over a Perch's. Perch 1, Pike 1.3,
        /// Tuna 1.5, Anglerfish 2.2, Magmafish 2.4, Northern salmon 2.6.
        /// </summary>
        public static float SpeciesScale(Fish fish) =>
            fish == null ? 1f : Mathf.Sqrt(Mathf.Max(PerchPull, fish.m_staminaUse) / PerchPull);

        /// <summary>The Fish component on an item's prefab, or null when the item is not a fish.</summary>
        public static Fish OfItem(ItemDrop.ItemData item)
        {
            GameObject prefab = item?.m_dropPrefab;
            if (prefab == null)
                return null;
            if (!fishOfPrefab.TryGetValue(prefab, out Fish fish))
                fishOfPrefab[prefab] = fish = prefab.GetComponent<Fish>();
            return fish;
        }

        public static bool IsFishItem(ItemDrop.ItemData item) => OfItem(item) != null;

        /// <summary>
        /// Every item prefab that is a fish (a Fish and an ItemDrop), the game's twelve and any a mod adds; read once per
        /// item database (<see cref="PrefabIndex"/>).
        /// </summary>
        public static IReadOnlyList<GameObject> Species() => PrefabIndex.Items().Fish;

        /// <summary>"a Pike", "an Anglerfish": the name with its English article.</summary>
        public static string WithArticle(string name) =>
            (name.Length > 0 && "AEIOUaeiou".IndexOf(name[0]) >= 0 ? "an " : "a ") + name;

        public static string Localize(string token) =>
            GameLocalization.instance == null ? token : GameLocalization.instance.Localize(token);
    }
}
