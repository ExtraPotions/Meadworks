// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Perks/ReplantSpace.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Whether a replanted sapling could grow on a spot, checked as the game checks a growing plant on its owner
    /// (Plant.UpdateHealth, from the plant's tenth second on):
    /// <list type="bullet">
    /// <item>the biome under it is one of the sapling's m_biome (tree saplings: Meadows, Black Forest and Plains; the firs
    /// also Mountain); the ground is cultivated if m_needCultivatedGround (no tree sapling needs it); it is not too hot
    /// (Ashlands) or too cold (Mountain, Deep North) unless it tolerates that or stands inside a shield generator's dome;</item>
    /// <item>no roof: nothing on Default, static_solid or piece straight above it within 100 m (HaveRoof);</item>
    /// <item>grow space: nothing on Default, static_solid, Default_small, piece or piece_nonsolid within m_growRadius
    /// (2 m; 3 m for the oak), other than a plant that is not healthy (HaveGrowSpace).</item>
    /// </list>
    /// Every tree sapling has m_destroyIfCantGrow: one that is not healthy when its grow time comes is destroyed. So a
    /// neighbouring trunk, a rock or a building in the way means no sapling at all rather than one that dies later.
    /// <para>The fell's own objects are left out: the tree (still standing until TreeBase.RPC_Damage deactivates it after
    /// SpawnLog), its log (just spawned upright in the trunk's place) and its stump (destroyed this frame, but Unity
    /// removes a destroyed object only at the end of the frame). The log will lie beside the sapling once it has fallen;
    /// <see cref="ReplantWait"/> keeps it from killing the sapling.</para>
    /// </summary>
    public static class ReplantSpace
    {
        /// <summary>Room for more colliders than the game's own grow-space check reads (30).</summary>
        private static readonly Collider[] Hits = new Collider[32];

        private static int spaceMask;
        private static int roofMask;

        /// <summary>The game's grow-space layers (Plant.HaveGrowSpace).</summary>
        public static int SpaceMask => spaceMask != 0 ? spaceMask
            : spaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");

        /// <summary>The game's roof layers (Plant.HaveRoof).</summary>
        private static int RoofMask => roofMask != 0 ? roofMask : roofMask = LayerMask.GetMask("Default", "static_solid", "piece");

        public static bool CanGrow(Plant sapling, Vector3 spot, FellContext fell) =>
            GroundSuits(sapling, spot) && !Roofed(spot, fell) && HasSpace(sapling, spot, fell);

        private static bool GroundSuits(Plant sapling, Vector3 spot)
        {
            Heightmap heightmap = Heightmap.FindHeightmap(spot);
            if (heightmap == null)
                return false;
            Heightmap.Biome biome = heightmap.GetBiome(spot);
            if ((biome & sapling.m_biome) == Heightmap.Biome.None)
                return false;
            if (sapling.m_needCultivatedGround && !heightmap.IsCultivated(spot))
                return false;
            return !TooHotOrCold(sapling, biome, spot);
        }

        private static bool TooHotOrCold(Plant sapling, Heightmap.Biome biome, Vector3 spot)
        {
            bool hot = !sapling.m_tolerateHeat && biome == Heightmap.Biome.AshLands;
            bool cold = !sapling.m_tolerateCold && (biome == Heightmap.Biome.DeepNorth || biome == Heightmap.Biome.Mountain);
            return (hot || cold) && !ShieldGenerator.IsInsideShield(spot);
        }

        private static bool Roofed(Vector3 spot, FellContext fell)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(spot, Vector3.up, 100f, RoofMask))
            {
                if (!IsFellOwn(hit.collider, fell))
                    return true;
            }
            return false;
        }

        private static bool HasSpace(Plant sapling, Vector3 spot, FellContext fell)
        {
            int count = Physics.OverlapSphereNonAlloc(spot, sapling.m_growRadius, Hits, SpaceMask);
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i];
                if (IsFellOwn(collider, fell))
                    continue;
                Plant plant = collider.GetComponent<Plant>();
                if (plant == null || plant.GetStatus() == Plant.Status.Healthy)
                    return false;
            }
            return true;
        }

        private static bool IsFellOwn(Collider collider, FellContext fell) =>
            Within(collider, fell.Tree) || Within(collider, fell.Log) || Within(collider, fell.Stub);

        private static bool Within(Collider collider, Component owner) =>
            owner != null && collider != null && collider.transform.IsChildOf(owner.transform);
    }
}
