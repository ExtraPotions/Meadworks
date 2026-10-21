// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// Which mountains hold a giant. A mountain is one of the game's own biome regions (its flood fill of the world into
    /// connected stretches of one biome, built from the world seed and the same on every machine) of a listed biome, at
    /// least <see cref="MinEdge"/> of the game's 12 m cells round its edge (about 150 m across): the snowy hilltops
    /// smaller than that do not count. Whether a mountain holds one is fixed by the world seed and the region, at the
    /// settings' `Mountains` share. Its giant comes once: the server records it in a global key as it spawns, so it never
    /// comes again, even after it dies.
    /// </summary>
    internal static class RimeMountains
    {
        public const int MinEdge = 40;
        private const string KeyPrefix = "ecp_rimegiant_";

        /// <summary>The mountain a point is on, if it holds a giant that has not come yet; -1 otherwise.</summary>
        public static int Waiting(Vector3 point)
        {
            AltBiomeWorldData? data = WorldGenerator.instance?.m_world?.m_biomeData;
            if (data == null || !data.IsReady || ZoneSystem.instance == null)
            {
                return -1;
            }
            BiomeSector sector = WorldGenerator.instance!.GetBiomeSector(point);
            int region = data.Sectors.IndexOf(sector);
            bool mountain = region >= 0 && sector.EdgeCount >= MinEdge && RimeGiantSettings.InBiome(sector.Biome);
            return mountain && Holds(region) && !ZoneSystem.instance.GetGlobalKey(KeyPrefix + region) ? region : -1;
        }

        /// <summary>The mountain's giant has come: recorded for good (on the server, for every player).</summary>
        public static void Claim(int region)
        {
            ZoneSystem.instance.SetGlobalKey(KeyPrefix + region);
            Log.Info($"Rime giant: mountain {region} wakes its giant.");
        }

        /// <summary>The seed and the region, hashed to a share from 0 to 1; below the settings' share, it holds a giant.</summary>
        private static bool Holds(int region)
        {
            uint hash = unchecked((uint)WorldGenerator.instance!.GetSeed() * 0x9E3779B1u ^ (uint)region * 0x85EBCA6Bu);
            hash ^= hash >> 16;
            hash = unchecked(hash * 0x7FEB352Du);
            hash ^= hash >> 15;
            hash = unchecked(hash * 0x846CA68Bu);
            hash ^= hash >> 16;
            return (hash & 0xFFFFFF) / (float)0x1000000 < RimeGiantSettings.Mountains;
        }
    }
}
