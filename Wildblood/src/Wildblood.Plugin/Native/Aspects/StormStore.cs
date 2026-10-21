// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// A Stormbound boss's latest storm, in its ZDO: when the lightning falls, on the shared clock, and the storm
    /// itself - its circles' radius, its damage and where each circle is. The owner writes it all at once and the game
    /// replicates it to every client holding the boss, so the storm needs no message of its own: each client takes it
    /// in when it changes, a client that meets the boss mid-storm reads it the same way, and a new owner reads when the
    /// last one fell. Every client draws and judges with the owner's numbers, never its own reading of the rules, so a
    /// client with unlocked or half-reloaded rules still sees and takes exactly the storm that was called. Only the
    /// codec lives here.
    /// </summary>
    internal static class StormStore
    {
        private static readonly int StormAtHash = TraitKeys.StormAt.GetStableHashCode();
        private static readonly int StormCirclesHash = TraitKeys.StormCircles.GetStableHashCode();

        /// <summary>A sanity cap on the circles one storm carries, whatever the player count.</summary>
        private const int MaxCircles = 32;

        /// <summary>Radius, damage and circle count come first: three 4-byte values.</summary>
        private const int Header = 12;

        /// <summary>Each circle centre: three floats.</summary>
        private const int PerCircle = 12;

        /// <summary>When the latest storm's lightning falls, in shared-clock ms; 0 before the first.</summary>
        public static long StrikeAt(ZDO zdo) => zdo.GetLong(StormAtHash);

        /// <summary>Owner only: the storm, then the time every machine watches for a change.</summary>
        public static void Write(ZDO zdo, long strikeAtMs, float radius, float damage, List<Vector3> centers)
        {
            ZPackage pkg = new ZPackage();
            int count = Mathf.Min(centers.Count, MaxCircles);
            pkg.Write(radius);
            pkg.Write(damage);
            pkg.Write(count);
            for (int i = 0; i < count; i++)
            {
                pkg.Write(centers[i]);
            }
            zdo.Set(TraitKeys.StormCircles, pkg.GetArray());
            zdo.Set(TraitKeys.StormAt, strikeAtMs);
        }

        /// <summary>
        /// The latest storm's circle centres, with the radius and damage (percent of maximum health) the owner called
        /// it with; no circles, and zeros, when there is none or the blob is unreadable.
        /// </summary>
        public static List<Vector3> Read(ZDO zdo, out float radius, out float damage)
        {
            List<Vector3> centers = new List<Vector3>();
            radius = 0f;
            damage = 0f;
            byte[]? bytes = zdo.GetByteArray(StormCirclesHash);
            if (bytes == null || bytes.Length < Header)
            {
                return centers;
            }
            ZPackage pkg = new ZPackage(bytes);
            radius = pkg.ReadSingle();
            damage = pkg.ReadSingle();
            int count = Mathf.Clamp(pkg.ReadInt(), 0, Mathf.Min(MaxCircles, (bytes.Length - Header) / PerCircle));
            for (int i = 0; i < count; i++)
            {
                centers.Add(pkg.ReadVector3());
            }
            return centers;
        }
    }
}
