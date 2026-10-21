// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/RockChunks.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The chunks of a multi-chunk rock, by area index (the index the game sends with every hit), for any machine.
    /// <list type="bullet">
    /// <item>A MineRock5 keeps one HitArea per child collider (m_hitAreas: collider, health, support bounds). Its owner
    /// saves every area's health in the ZDO as one package (ZDOVars.s_health) and tells everybody each broken area
    /// (RPC_SetAreaHealth); other machines reload the package every 10 s (CheckForUpdate), so a non-owner's healths
    /// can lag. A broken area's collider object is inactive.</item>
    /// <item>A MineRock keeps colliders only (m_hitAreas, filled in Start) and each area's health in the ZDO as
    /// "Health" + index, defaulting to its full health.</item>
    /// <item>A single piece has one "chunk", index 0, whose health is the Destructible's (ZDOVars.s_health).</item>
    /// </list>
    /// </summary>
    public static class RockChunks
    {
        /// <summary>How many chunks the rock has, broken ones included.</summary>
        public static int Count(Rock rock)
        {
            switch (rock?.Target)
            {
                case MineRock5 chunks:
                    return chunks.m_hitAreas != null ? chunks.m_hitAreas.Count : 0;
                case MineRock chunks:
                    return chunks.m_hitAreas != null ? chunks.m_hitAreas.Length : 0;
                case Destructible _:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>A chunk's health as this machine knows it; 0 when broken or unknown.</summary>
        public static float Health(Rock rock, int area)
        {
            if (rock == null || !rock.IsValid || area < 0 || area >= Count(rock))
                return 0f;
            switch (rock.Target)
            {
                case MineRock5 chunks:
                    return chunks.m_hitAreas[area].m_health;
                case MineRock chunks:
                    return rock.View.GetZDO().GetFloat("Health" + area, chunks.GetHealth());
                case Destructible piece:
                    return rock.View.GetZDO().GetFloat(ZDOVars.s_health, FullHealth(piece.m_health));
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// A MineRock5 chunk's health as the rock's owner last saved it in the ZDO (the package MineRock5.LoadHealth
        /// reads), which reaches a machine that is not the owner long before it reloads its own healths;
        /// <see cref="Health"/> for any other rock and while nothing is saved yet.
        /// </summary>
        public static float SavedHealth(Rock rock, int area)
        {
            bool saves = rock?.Target is MineRock5 && rock.IsValid && area >= 0;
            string saved = saves ? rock.View.GetZDO().GetString(ZDOVars.s_health) : "";
            if (saved.Length == 0)
                return Health(rock, area);
            ZPackage package = new ZPackage(Convert.FromBase64String(saved));
            if (area >= package.ReadInt())
                return Health(rock, area);
            for (int skipped = 0; skipped < area; skipped++)
                package.ReadSingle();
            return package.ReadSingle();
        }

        /// <summary>A rock's full health at the world's level, as MineRock5, MineRock and Destructible all compute it.</summary>
        public static float FullHealth(float baseHealth) =>
            Game.instance == null ? baseHealth : baseHealth + Game.m_worldLevel * baseHealth * Game.instance.m_worldLevelMineHPMultiplier;

        public static bool IsIntact(Rock rock, int area) => Health(rock, area) > 0f;

        /// <summary>How many chunks are still intact.</summary>
        public static int Left(Rock rock)
        {
            int count = Count(rock);
            int left = 0;
            for (int area = 0; area < count; area++)
            {
                if (IsIntact(rock, area))
                    left++;
            }
            return left;
        }

        /// <summary>A chunk's collider; null for a single piece or a bad index.</summary>
        public static Collider ColliderOf(Rock rock, int area)
        {
            if (area < 0 || area >= Count(rock))
                return null;
            switch (rock.Target)
            {
                case MineRock5 chunks:
                    return chunks.m_hitAreas[area].m_collider;
                case MineRock chunks:
                    return chunks.m_hitAreas[area];
                default:
                    return null;
            }
        }

        /// <summary>The centre of a chunk's collider bounds (where the game spawns a MineRock5 chunk's drops); the rock's position for a single piece.</summary>
        public static Vector3 Centre(Rock rock, int area)
        {
            Collider collider = ColliderOf(rock, area);
            return collider != null ? collider.bounds.center : rock.Position;
        }

        /// <summary>The area index of a chunk collider, as the game finds it; -1 when it is none of the rock's chunks.</summary>
        public static int AreaOf(Rock rock, Collider collider)
        {
            if (collider == null)
                return -1;
            switch (rock?.Target)
            {
                case MineRock5 chunks:
                    return chunks.m_hitAreas != null ? chunks.GetAreaIndex(collider) : -1;
                case MineRock chunks:
                    return chunks.m_hitAreas != null ? chunks.GetAreaIndex(collider) : -1;
                default:
                    return -1;
            }
        }
    }
}
