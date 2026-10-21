// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/Crops.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Crops belong to the Farming module, never to Foraging: a pickable whose prefab name is in any Plant's
    /// m_grownPrefabs, planted or wild (jotun puffs, magecap and the seed carrots, turnips and onions grow wild from the
    /// same prefabs; vines too, though Farming leaves them to the game). Filled from the scene's prefabs when ZNetScene
    /// wakes (<see cref="PrefabIndex"/>), so plants other mods register in their own Awake postfix count too.
    /// </summary>
    public static class Crops
    {
        private static readonly HashSet<string> names = new HashSet<string>();

        /// <summary>Whether this pickable is a crop (its prefab grows from a plant).</summary>
        public static bool IsCrop(Pickable pickable) => pickable != null && names.Contains(Utils.GetPrefabName(pickable.gameObject));

        /// <summary>Whether a pickable prefab of this name is a crop.</summary>
        public static bool IsCropPrefab(string prefabName) => prefabName != null && names.Contains(prefabName);

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        private static class SceneAwake
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                foreach (Plant plant in PrefabIndex.Scene().Plants)
                    Add(plant);
            }
        }

        private static void Add(Plant plant)
        {
            if (plant?.m_grownPrefabs == null)
                return;
            foreach (GameObject grown in plant.m_grownPrefabs)
            {
                if (grown != null && grown.GetComponent<Pickable>() != null)
                    names.Add(grown.name);
            }
        }
    }
}
