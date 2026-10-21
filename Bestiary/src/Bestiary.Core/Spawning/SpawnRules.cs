// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later: EliteCreaturesPack 0.7.2 at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (Mimic/CryptSwap.cs, Core/NameList.cs, Slinger/SlingerSpawns.cs,
// Kraken/KrakenSpawns.cs, Headsman/HeadsmanChambers.cs, Skeletons/SkeletonDraw.cs, RimeGiant/RimeMountains.cs).
// Changed by expDARE, 2026: decisions restated as Unity-free functions of their inputs (the roll, seed and world
// facts are passed in), so each rule is tested without the game.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Config;

namespace ExtraPotions.Bestiary.Spawning
{
    /// <summary>A comma list of prefab names, trimmed and case-sensitive (ECP <c>NameList</c>); empty entries dropped.</summary>
    public sealed class NameSet
    {
        private readonly HashSet<string> names;

        private NameSet(HashSet<string> names) => this.names = names;

        public static NameSet Parse(string? text) =>
            new NameSet(new HashSet<string>((text ?? "").Split(',').Select(n => n.Trim()).Where(n => n.Length > 0), StringComparer.Ordinal));

        public bool Contains(string name) => names.Contains(name);

        public int Count => names.Count;
    }

    public static class SpawnRules
    {
        /// <summary>Metres within which a crewed ship must be for a Kraken to spawn (ECP <c>KrakenSpawns.ShipNear</c>).</summary>
        public const float KrakenShipRange = 150f;

        /// <summary>Metres within which no second Rime Giant or Kraken may be loaded (spawn entry <c>m_spawnDistance</c>).</summary>
        public const float ApartDistance = 1000f;

        /// <summary>Mixed into a burial chamber's seed for the Executioner roll (ECP "HSMN").</summary>
        public const int ChamberSalt = 0x48534d4e;

        /// <summary>
        /// A crypt chest becomes a dormant mimic when its crypt first fills: module on, chest listed, and the roll (0 to 1)
        /// under the chance. Decided by the chest owner and written to the chest ZDO.
        /// </summary>
        public static bool IsMimic(bool enabled, NameSet chests, string chestPrefab, float chancePercent, double roll) =>
            enabled && chests.Contains(chestPrefab) && roll < chancePercent / 100.0;

        /// <summary>
        /// A spawning Greydwarf (wild, or from a nest when nests are allowed) in a listed biome becomes a slinger at the
        /// share. Camps and other fixed spawners keep their greydwarfs (the caller only asks for wild and nest spawns).
        /// </summary>
        public static bool IsSlinger(bool enabled, string prefab, bool fromNest, bool nestsAllowed, BiomeMask biomes, BiomeMask here,
            float sharePercent, double roll)
        {
            if (!enabled || (fromNest && !nestsAllowed) || prefab != "Greydwarf" || sharePercent <= 0f) return false;
            return (biomes & here) != 0 && roll < sharePercent / 100.0;
        }

        /// <summary>The Kraken spawn goes ahead over water at least <paramref name="minDepth"/> deep, with a crewed ship near, alone.</summary>
        public static bool KrakenMaySpawn(bool enabled, float depth, float minDepth, bool crewedShipNear, bool anotherKrakenNear) =>
            enabled && depth >= minDepth && crewedShipNear && !anotherKrakenNear;

        /// <summary>Whether a burial chamber holds an Executioner: its seed decides, once (ECP <c>System.Random(seed ^ Salt)</c>).</summary>
        public static bool ExecutionerChamber(int dungeonSeed, float chambersPercent) =>
            new Random(dungeonSeed ^ ChamberSalt).NextDouble() < chambersPercent / 100.0;
    }

    /// <summary>The arsenal switches (section 4.6): master, per skeleton title, crossbowman.</summary>
    public sealed class SkeletonSwitches
    {
        private readonly IReadOnlyDictionary<string, bool> titles;

        public SkeletonSwitches(bool master, IReadOnlyDictionary<string, bool> titles, bool crossbowman)
        {
            Master = master;
            this.titles = titles;
            Crossbowman = crossbowman;
        }

        public bool Master { get; }
        public bool Crossbowman { get; }

        public bool Spawns(string title) => Master && titles.TryGetValue(title, out var on) && on;

        public bool CrossbowmanSpawns => Master && Crossbowman;
    }

    /// <summary>
    /// One even draw for every spawn of the Black Forest skeleton (wild, spawners, bone piles): the plain skeleton, each
    /// arsenal skeleton switched on and the crossbowman. A no-archer skeleton draws among the melee kinds only. Decided by
    /// whoever runs the spawner; the spawn keeps its levels.
    /// </summary>
    public static class SkeletonDraw
    {
        public const string Archer = "Skeleton", NoArcher = "Skeleton_NoArcher";

        private static readonly string[] Titles = { "Cutthroat", "Swordsman", "Axeman", "Bonebreaker", "Spearman", "Halberdier", "Bowman" };

        /// <summary>The creatures that may come in place of <paramref name="source"/>, in ECP order; empty for any other prefab.</summary>
        public static IReadOnlyList<string> Candidates(string source, SkeletonSwitches switches)
        {
            var list = new List<string>();
            var archer = source == Archer;
            if (!archer && source != NoArcher) return list;
            foreach (var title in Titles)
                if ((archer || title != "Bowman") && switches.Spawns(title))
                    list.Add("ECP_Skeleton" + title);
            if (archer && switches.CrossbowmanSpawns) list.Add("ECP_SkeletonCrossbowman");
            return list;
        }

        /// <summary>Slots of the draw: every candidate and the skeleton itself.</summary>
        public static int Slots(string source, SkeletonSwitches switches) => Candidates(source, switches).Count + 1;

        /// <summary>
        /// The creature drawn in place of <paramref name="source"/>, or null to keep it. <paramref name="pick"/> returns
        /// an index from 0 to its argument minus one (the game's <c>Random.Range(0, n)</c>).
        /// </summary>
        public static string? Instead(string source, SkeletonSwitches switches, Func<int, int> pick)
        {
            var others = Candidates(source, switches);
            if (others.Count == 0) return null;
            var drawn = pick(others.Count + 1);
            return drawn >= 0 && drawn < others.Count ? others[drawn] : null;
        }
    }

    /// <summary>
    /// Which mountains hold a Rime Giant: a biome region of a listed biome with at least <see cref="MinEdge"/> of the
    /// game's 12 m edge cells (about 150 m across), chosen by the world seed and region at the settings' share. Its giant
    /// comes once: the server sets global key <c>ecp_rimegiant_&lt;region&gt;</c> as it spawns.
    /// </summary>
    public static class RimeMountains
    {
        public const int MinEdge = 40;

        public static string ClaimKey(int region) => BestiaryKeys.RimeGiantClaimPrefix + region;

        public static bool Eligible(int edgeCount, BiomeMask biome, BiomeMask biomes) => edgeCount >= MinEdge && (biomes & biome) != 0;

        /// <summary>ECP's integer hash of seed and region, low 24 bits.</summary>
        public static uint Hash24(int worldSeed, int region)
        {
            unchecked
            {
                uint hash = (uint)worldSeed * 0x9E3779B1u ^ (uint)region * 0x85EBCA6Bu;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash & 0xFFFFFF;
            }
        }

        /// <summary>The region's share from 0 (inclusive) to 1 (exclusive).</summary>
        public static float Share(int worldSeed, int region) => Hash24(worldSeed, region) / (float)0x1000000;

        public static bool Holds(int worldSeed, int region, float mountainsPercent) => Share(worldSeed, region) < mountainsPercent / 100f;

        /// <summary>A spawn on this region may go ahead: an eligible mountain that holds a giant whose claim is not set.</summary>
        public static bool Waiting(int region, int edgeCount, BiomeMask biome, BiomeMask biomes, bool holds, bool claimed) =>
            region >= 0 && Eligible(edgeCount, biome, biomes) && holds && !claimed;
    }
}
