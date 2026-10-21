// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Perks/ReplantSaplings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Which sapling grows which tree, read from the game's own saplings: every prefab registered in ZNetScene
    /// (m_namedPrefabs) with a Plant whose m_grownPrefabs holds a tree (TreeBase). Built the first time a tree falls in a
    /// session, and again for a new ZNetScene, so saplings other mods register count too.
    /// <para>A felled tree finds its sapling by its prefab name, else by its kind: its log's prefab name
    /// (<see cref="FellContext.Species"/>), so a variant without a sapling of its own gets the sapling of its kind. In the
    /// game: Beech_Sapling grows Beech1; Birch_Sapling grows Birch1 and Birch2, and by kind stands in for Birch1_aut and
    /// Birch2_aut; Oak_Sapling grows Oak1; PineTree_Sapling Pinetree_01; FirTree_Sapling FirTree; FirTree_big_Sapling
    /// FirTree_big. The swamp tree, the snow pines and firs, the Yggdrasil shoots and the Ashlands trees have none.</para>
    /// </summary>
    public static class ReplantSaplings
    {
        private static readonly Dictionary<string, Plant> ByTree = new Dictionary<string, Plant>();
        private static readonly Dictionary<string, Plant> ByKind = new Dictionary<string, Plant>();
        private static ZNetScene scene;

        /// <summary>The sapling that grows this tree, else one that grows a tree of its kind; null when the game has none.</summary>
        public static Plant For(string treePrefab, string species)
        {
            Refresh();
            if (!string.IsNullOrEmpty(treePrefab) && ByTree.TryGetValue(treePrefab, out Plant sapling))
                return sapling;
            return !string.IsNullOrEmpty(species) && ByKind.TryGetValue(species, out sapling) ? sapling : null;
        }

        /// <summary>Reads the saplings once per ZNetScene; with no ZNetScene the tables stay empty.</summary>
        private static void Refresh()
        {
            if (scene == ZNetScene.instance)
                return;
            scene = ZNetScene.instance;
            ByTree.Clear();
            ByKind.Clear();
            if (scene == null)
                return;
            foreach (GameObject prefab in scene.m_namedPrefabs.Values)
                Add(prefab != null ? prefab.GetComponent<Plant>() : null);
        }

        private static void Add(Plant sapling)
        {
            if (sapling == null || sapling.m_grownPrefabs == null)
                return;
            foreach (GameObject grown in sapling.m_grownPrefabs)
            {
                TreeBase tree = grown != null ? grown.GetComponent<TreeBase>() : null;
                if (tree == null)
                    continue;
                Keep(ByTree, grown.name, sapling);
                if (tree.m_logPrefab != null)
                    Keep(ByKind, tree.m_logPrefab.name, sapling);
            }
        }

        /// <summary>The first sapling found for a key stays.</summary>
        private static void Keep(Dictionary<string, Plant> table, string key, Plant sapling)
        {
            if (!table.ContainsKey(key))
                table[key] = sapling;
        }
    }
}
