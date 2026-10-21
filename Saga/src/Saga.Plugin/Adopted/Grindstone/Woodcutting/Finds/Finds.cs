// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Finds/Finds.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A felled tree can hide something: a bird's nest, a wild hive, a few coins. Runs on the tree's ZDO owner, where
    /// TreeBase.RPC_Damage spawns the log and drops the canopy items (<see cref="Felling"/>), for every tree felled by a
    /// woodcutter, domino fells included. The chance grows linearly with the woodcutter's level from "Find Chance At
    /// 0" to "Find Chance At 100"; the find is picked by weight from Saga.Finds.yml
    /// (<see cref="FindFile"/>), its items drop a few metres up the trunk on the woodcutter's side, away from the
    /// falling log (<see cref="FindSpawner"/>), and everybody near sees "Found &lt;name&gt;!" there
    /// (<see cref="WoodCallout"/>). The drops are networked objects owned by this machine, so every client sees them.
    /// </summary>
    public static class Finds
    {
        /// <summary>How far up the trunk the items appear, in metres at the tree's own scale.</summary>
        private const float Height = 3f;

        /// <summary>How far from the trunk's centre, in metres at the tree's own scale: clear of the log.</summary>
        private const float Offset = 1.5f;

        /// <summary>Called by <see cref="Felling"/> on the tree's owner, after Replanting.</summary>
        public static void OnFelled(FellContext fell)
        {
            Woodcutter woodcutter = fell.Woodcutter;
            if (woodcutter == null || Random.value >= Chance(woodcutter.Factor))
                return;
            FindEntry find = FindFile.Pick(fell.TreePrefab, fell.Biome);
            if (find == null)
                return;
            Vector3 away = Away(fell.HitDir);
            Vector3 spot = fell.Position + Vector3.up * (Height * fell.Scale.y) + away * (Offset * fell.Scale.x);
            if (FindSpawner.Spawn(find, spot, away) > 0)
                WoodCallout.Broadcast(spot, SagaText.Packet("gs_2dadc5689ae69efb", find.Name));
        }

        /// <summary>The chance of a find, 0..1, for a woodcutter at this share of level 100.</summary>
        public static float Chance(float factor) =>
            Mathf.Clamp01(Mathf.Lerp(FindSettings.ChanceAt0.Value, FindSettings.ChanceAt100.Value, factor) / 100f);

        /// <summary>
        /// Level ground direction opposite the felling hit: the hit pushes the log over along its direction, so the
        /// other side is clear, and it faces the woodcutter. A random direction when the hit has none.
        /// </summary>
        private static Vector3 Away(Vector3 hitDir)
        {
            Vector3 away = new Vector3(-hitDir.x, 0f, -hitDir.z);
            if (away.sqrMagnitude > 0.01f)
                return away.normalized;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }
}
