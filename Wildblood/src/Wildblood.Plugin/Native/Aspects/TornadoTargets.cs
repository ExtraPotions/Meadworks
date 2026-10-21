// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Runtime;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Who a Nightfall wave hunts, where each tornado rises, and where the ground is under one. Every living player
    /// within `range` of the boss (<see cref="StormTargets.InRange"/>) gets one tornado, unless a live tornado - this
    /// boss's or another Nightfall boss's nearby, such as a twin's - is already hunting them: one tornado per player at
    /// a time. It rises <see cref="MinSpawn"/> to <see cref="MaxSpawn"/> m from its player, within
    /// <see cref="SpawnArc"/> degrees of the way they face, so they see it whirl up and have time to move before it
    /// has formed. A tornado runs along the terrain, or on the water's surface where the terrain is under the sea - a
    /// waterspout - never over buildings or ships; its funnel is tall enough to reach a player standing on them. In an
    /// interior it runs along the floor.
    /// </summary>
    internal static class TornadoTargets
    {
        /// <summary>Degrees either side of a player's facing a tornado may rise at.</summary>
        private const float SpawnArc = 60f;

        private const float MinSpawn = 6f;
        private const float MaxSpawn = 9f;

        /// <summary>The height above which a place is an interior, as the game itself draws the line.</summary>
        private const float Interior = 3000f;

        /// <summary>How far up an interior floor may step, and how far down it is looked for, from the last one.</summary>
        private const float Climb = 2f;
        private const float Drop = 12f;

        /// <summary>A wave on the players in reach, its numbers fixed now, the next <paramref name="next"/> s later.</summary>
        public static TornadoWave Wave(List<Player> players, TornadoSettings settings, float next)
        {
            TornadoWave wave = new TornadoWave
            {
                Form = settings.Form,
                Life = settings.Life,
                Speed = settings.Speed / 100f * PlayerSpeed.Reference(),
                Damage = settings.Damage,
                Next = next,
                Shape = settings.Shape,
            };
            foreach (Player player in players)
            {
                if (!NightfallStorm.Hunted(player.GetZDOID()))
                {
                    wave.Add(player.GetZDOID(), SpawnNear(player));
                }
            }
            return wave;
        }

        /// <summary>A few metres from the player, roughly in front, at their feet's height (each machine grounds it).</summary>
        private static Vector3 SpawnNear(Player player)
        {
            Vector3 facing = StormTargets.Flat(player.transform.forward);
            if (facing == Vector3.zero)
            {
                facing = Vector3.forward;
            }
            Vector3 way = Quaternion.Euler(0f, Random.Range(-SpawnArc, SpawnArc), 0f) * facing;
            return player.transform.position + way * Random.Range(MinSpawn, MaxSpawn);
        }

        /// <summary>
        /// The ground under a point: the terrain, or the sea's surface where that is higher; <paramref name="fallback"/>
        /// where this machine has no terrain there yet. Interiors (the Queen's hall, any boss fought in a dungeon) hang
        /// far above the world, where there is no terrain under them: there it is the floor a little below or above
        /// <paramref name="fallback"/>.
        /// </summary>
        public static float Ground(Vector3 at, float fallback)
        {
            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null)
            {
                return fallback;
            }
            if (fallback > Interior)
            {
                return StormTargets.FloorY(at, fallback, Climb, Drop);
            }
            float y = zones.GetGroundHeight(at, out float terrain) ? terrain : fallback;
            return Mathf.Max(y, zones.m_waterLevel);
        }
    }
}
