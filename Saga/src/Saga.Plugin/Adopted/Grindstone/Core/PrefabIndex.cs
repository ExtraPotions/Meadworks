// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/PrefabIndex.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// One walk over the scene's prefabs (ZNetScene.m_prefabs) and one over the item database's items (ObjectDB.m_items)
    /// per world load, shared by every discovery that looks for a component there: crops and the crop catalog, kitchens
    /// and what they use, animal meat, old growth sizes, eggs and fish. Several of those run after both ZNetScene.Awake
    /// and ObjectDB.Awake and some more than once, and each used to walk the lists itself. A list is read the first time
    /// it is asked for and kept while the scene or database still has that same list at the same length (a new world's
    /// scene has a list of its own), so prefabs another mod adds later (a longer list) are read on the next ask. What is
    /// found keeps the prefabs' own order. Runs on whichever machine asks, the dedicated server included.
    /// </summary>
    public static class PrefabIndex
    {
        /// <summary>A prefab that converts items: its cooking station, fermenter and smelter, any of which may be null.</summary>
        public struct Converter
        {
            public GameObject Prefab;
            public CookingStation Station;
            public Fermenter Fermenter;
            public Smelter Smelter;
        }

        /// <summary>The scene's prefabs that discoveries look for, by component.</summary>
        public sealed class ScenePrefabs
        {
            public readonly List<Plant> Plants = new List<Plant>();
            public readonly List<TreeBase> Trees = new List<TreeBase>();
            public readonly List<Converter> Converters = new List<Converter>();

            /// <summary>The CharacterDrop of every prefab with a Tameable.</summary>
            public readonly List<CharacterDrop> TameableDrops = new List<CharacterDrop>();
        }

        /// <summary>The item database's prefabs that discoveries look for.</summary>
        public sealed class ItemPrefabs
        {
            /// <summary>Item prefabs with a Fish and an ItemDrop.</summary>
            public readonly List<GameObject> Fish = new List<GameObject>();

            /// <summary>The ItemDrop of every item prefab with an EggGrow.</summary>
            public readonly List<ItemDrop> Eggs = new List<ItemDrop>();
        }

        private static ScenePrefabs scenePrefabs = new ScenePrefabs();
        private static List<GameObject> sceneList;
        private static int sceneCount = -1;

        private static ItemPrefabs itemPrefabs = new ItemPrefabs();
        private static List<GameObject> itemList;
        private static int itemCount = -1;

        /// <summary>The current scene's prefabs; the last ones read while there is no scene.</summary>
        public static ScenePrefabs Scene()
        {
            List<GameObject> prefabs = ZNetScene.instance != null ? ZNetScene.instance.m_prefabs : null;
            if (prefabs == null || (ReferenceEquals(prefabs, sceneList) && prefabs.Count == sceneCount))
                return scenePrefabs;
            ScenePrefabs found = new ScenePrefabs();
            foreach (GameObject prefab in prefabs)
            {
                if (prefab != null)
                    AddScenePrefab(prefab, found);
            }
            scenePrefabs = found;
            sceneList = prefabs;
            sceneCount = prefabs.Count;
            return found;
        }

        /// <summary>The current item database's prefabs; the last ones read while there is no database.</summary>
        public static ItemPrefabs Items()
        {
            List<GameObject> items = ObjectDB.instance != null ? ObjectDB.instance.m_items : null;
            if (items == null || (ReferenceEquals(items, itemList) && items.Count == itemCount))
                return itemPrefabs;
            ItemPrefabs found = new ItemPrefabs();
            foreach (GameObject prefab in items)
            {
                if (prefab != null)
                    AddItemPrefab(prefab, found);
            }
            itemPrefabs = found;
            itemList = items;
            itemCount = items.Count;
            return found;
        }

        private static void AddScenePrefab(GameObject prefab, ScenePrefabs found)
        {
            Plant plant = prefab.GetComponent<Plant>();
            if (plant != null)
                found.Plants.Add(plant);
            TreeBase tree = prefab.GetComponent<TreeBase>();
            if (tree != null)
                found.Trees.Add(tree);
            Converter converter = new Converter
            {
                Prefab = prefab,
                Station = prefab.GetComponent<CookingStation>(),
                Fermenter = prefab.GetComponent<Fermenter>(),
                Smelter = prefab.GetComponent<Smelter>(),
            };
            if (converter.Station != null || converter.Fermenter != null || converter.Smelter != null)
                found.Converters.Add(converter);
            CharacterDrop drops = prefab.GetComponent<Tameable>() != null ? prefab.GetComponent<CharacterDrop>() : null;
            if (drops != null)
                found.TameableDrops.Add(drops);
        }

        private static void AddItemPrefab(GameObject prefab, ItemPrefabs found)
        {
            if (prefab.GetComponent<Fish>() != null && prefab.GetComponent<ItemDrop>() != null)
                found.Fish.Add(prefab);
            if (prefab.GetComponent<EggGrow>() != null)
                found.Eggs.Add(prefab.GetComponent<ItemDrop>());
        }
    }
}
