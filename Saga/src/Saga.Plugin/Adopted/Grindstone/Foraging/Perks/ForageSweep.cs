// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Foraging/Perks/ForageSweep.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Sweep picking, on the picker's client: from Sweep Level, a pick of forage also picks every plant of the same
    /// kind (the same prefab) within level / 100 × Sweep Radius At 100 of it. Each is the game's own pick
    /// (Pickable.Interact, without the interact animation), so each goes through <see cref="ForagePick"/> for its
    /// experience, extra yield and stars, and reaches its own owner. Plants are found with the colliders the player
    /// can interact with; crops and other modules' plants are never forage, so they are never swept.
    /// </summary>
    public static class ForageSweep
    {
        private static readonly Collider[] hits = new Collider[256];

        public static bool Running { get; private set; }

        public static void Around(Pickable origin, Player player)
        {
            if (Running || origin == null || player == null || player != Player.m_localPlayer)
                return;
            float radius = Radius(ForagingSkill.Level(player));
            if (radius <= 0f)
                return;
            List<Pickable> plants = Neighbours(origin, radius, player.m_interactMask);
            Running = true;
            try
            {
                foreach (Pickable plant in plants)
                    plant.Interact(player, false, false);
            }
            finally
            {
                Running = false;
            }
        }

        /// <summary>How far a sweep reaches at a level; 0 below Sweep Level.</summary>
        public static float Radius(float level)
        {
            if (level < ForagePerkSettings.SweepLevel.Value)
                return 0f;
            return Mathf.Max(0f, ForagePerkSettings.SweepRadiusAt100.Value) * Mathf.Clamp01(level / ForagingSkill.MaxLevel);
        }

        private static List<Pickable> Neighbours(Pickable origin, float radius, int mask)
        {
            string kind = Utils.GetPrefabName(origin.gameObject);
            int count = Physics.OverlapSphereNonAlloc(origin.transform.position, radius, hits, mask, QueryTriggerInteraction.Collide);
            HashSet<Pickable> seen = new HashSet<Pickable> { origin };
            List<Pickable> plants = new List<Pickable>();
            for (int i = 0; i < count; i++)
            {
                Pickable plant = hits[i] != null ? hits[i].GetComponentInParent<Pickable>() : null;
                if (plant != null && seen.Add(plant) && Utils.GetPrefabName(plant.gameObject) == kind && Forage.CanPick(plant) && plant.CanBePicked())
                    plants.Add(plant);
            }
            return plants;
        }
    }
}
