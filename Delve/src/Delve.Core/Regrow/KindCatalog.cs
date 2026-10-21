// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0: the kind
// tables in src/RestockRegenPlugin.cs (generators, prefab names, ZDO keys, boss place) and src/Regrow.cs Configure (world
// object prefabs, drifting flametal, damaged forms "_destruction" and "_frac" from src/DungeonRegrow.cs and Regrow.cs)).
// The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: one table for every Delve kind, with the restore style (respawn from a remembered spot or
// clear a picked marker), the default radii of design 4.5 and the biome of each world kind's entry notice.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Regrow
{
    /// <summary>How a kind comes back.</summary>
    public enum RestoreStyle
    {
        /// <summary>Created again at its remembered spot; a damaged one is replaced.</summary>
        Respawn,
        /// <summary>The object stays when taken, marked picked; restoring clears the mark.</summary>
        Unpick,
    }

    public sealed class KindInfo
    {
        internal KindInfo(DelveKind kind, string? generator, string[] whole, string[] damagedSuffixes, string? biome, bool drifts,
            float entranceRadius, float insideRadius, string? spotsKey, string? visitKey, string[]? unpick = null)
        {
            Kind = kind;
            Generator = generator;
            Whole = Array.AsReadOnly(whole);
            DamagedSuffixes = Array.AsReadOnly(damagedSuffixes);
            Biome = biome;
            Drifts = drifts;
            EntranceRadius = entranceRadius;
            InsideRadius = insideRadius;
            RestockRegenSpotsKey = spotsKey;
            RestockRegenVisitKey = visitKey;
            UnpickPrefabs = new HashSet<string>(unpick ?? Array.Empty<string>(), StringComparer.Ordinal);
            Style = UnpickPrefabs.Count == whole.Length && whole.Length > 0 ? RestoreStyle.Unpick : RestoreStyle.Respawn;
        }

        public DelveKind Kind { get; }

        /// <summary>The dungeon generator prefab of a dungeon kind; null for world kinds.</summary>
        public string? Generator { get; }

        /// <summary>The whole prefabs this kind brings back.</summary>
        public IReadOnlyList<string> Whole { get; }

        /// <summary>Suffixes of the damaged forms a hit leaves in place of a whole prefab.</summary>
        public IReadOnlyList<string> DamagedSuffixes { get; }

        /// <summary>The biome whose entry notice names this world kind (design D3); null for dungeon kinds.</summary>
        public string? Biome { get; }

        /// <summary>Floats and drifts (flametal on lava): its spot matches flat within 6 m.</summary>
        public bool Drifts { get; }

        /// <summary>Default entrance radius (design 4.5, <c>Delve.Sites.yml</c>); for world kinds 0 (their Visit Radius setting applies).</summary>
        public float EntranceRadius { get; }

        /// <summary>Default inside radius around the generator.</summary>
        public float InsideRadius { get; }

        /// <summary>Overall style; <see cref="KindCatalog.StyleOf"/> answers per prefab for mixed kinds such as Morkhalla.</summary>
        public RestoreStyle Style { get; }

        /// <summary>RestockRegen's ZDO key for this kind's remembered spots on the generator (converter).</summary>
        public string? RestockRegenSpotsKey { get; }

        /// <summary>RestockRegen's ZDO key for the generator's last visit day + 1 (converter).</summary>
        public string? RestockRegenVisitKey { get; }

        internal HashSet<string> UnpickPrefabs { get; }
    }

    public static class KindCatalog
    {
        static readonly string[] None = Array.Empty<string>();

        static readonly KindInfo[] Kinds =
        {
            new KindInfo(DelveKind.MudPiles, "DG_SunkenCrypt", new[] { "mudpile2", "mudpile" }, new[] { "_frac" }, null, false, 30, 200,
                "restockregen_mudspots", "restockregen_lastvisit"),
            new KindInfo(DelveKind.FrostCaveCrystals, "DG_Cave", new[] { "Pickable_MountainCaveCrystal" }, None, null, false, 50, 200,
                "restockregen_crystalspots", "restockregen_cavevisit"),
            new KindInfo(DelveKind.BlackCores, "DG_DvergrTown", new[] { "Pickable_BlackCoreStand" }, None, null, false, 50, 200,
                "restockregen_corespots", "restockregen_minevisit", unpick: new[] { "Pickable_BlackCoreStand" }),
            new KindInfo(DelveKind.Morkhalla, "DG_MorkHalla", new[]
                {
                    "Morkhalla_Eye1", "Morkhalla_Eye2", "Morkhalla_Eye3", "Morkhalla_Eye4",
                    "Morkhalla_Eye5_gemstone", "Morkhalla_Eye6_gemstone", "Morkhalla_Eye7_gemstone",
                    "Morkhalla_Rubble1", "Morkhalla_Rubble2", "Morkhalla_Rubble3", "Morkhalla_Rubble4",
                    "Pickable_MorkHallaTreasure", "BlackIce_Start",
                }, None, null, false, 50, 200, "restockregen_morkspots", "restockregen_morkvisit",
                unpick: new[] { "Morkhalla_Eye1", "Morkhalla_Eye2", "Morkhalla_Eye3", "Morkhalla_Eye4", "Morkhalla_Eye5_gemstone", "Morkhalla_Eye6_gemstone", "Morkhalla_Eye7_gemstone" }),
            new KindInfo(DelveKind.Hole, "DG_Hole", new[] { "elaking_trashpile", "Spawner_Hole", "Spawner_Hole_double" }, None, null, false, 40, 150,
                "restockregen_holespots", "restockregen_holevisit"),
            new KindInfo(DelveKind.AncientArmor, null, new[] { "giant_helmet1", "giant_helmet2", "giant_sword1", "giant_sword2" }, new[] { "_destruction" },
                "Mistlands", false, 0, 0, null, null),
            new KindInfo(DelveKind.Obsidian, null, new[] { "MineRock_Obsidian" }, None, "Mountain", false, 0, 0, null, null),
            new KindInfo(DelveKind.Flametal, null, new[] { "LeviathanLava" }, None, "AshLands", true, 0, 0, null, null),
            new KindInfo(DelveKind.Ice, null, new[] { "IcePond_rock", "IceShore_1" }, new[] { "_frac" }, "DeepNorth", false, 0, 0, null, null),
            new KindInfo(DelveKind.Queen, "DG_DvergrBoss", new[]
                {
                    "SeekerEgg", "blackmarble_creep_4x1x1", "blackmarble_creep_4x2x1", "blackmarble_creep_stair",
                    "blackmarble_creep_slope_inverted_1x1x2", "blackmarble_creep_slope_inverted_2x2x1",
                }, None, null, false, 30, 150, "restockregen_citadelspots", "restockregen_citadelvisit"),
        };

        /// <summary>The Queen's place (RestockRegen 0.16.0): her room, her offset in that room and her turn, and the sealed door.</summary>
        public static class Citadel
        {
            public const string Boss = "SeekerQueen";
            public const string BossRoom = "dvergr_new_bossroom_ENTRANCE02";
            public const string Door = "dungeon_queen_door";
            public static readonly Vec3 BossOffset = new Vec3(2f, -24f, 0f);
            public const float BossYaw = 270f;
        }

        /// <summary>The regrow kinds this catalog covers (C3, C4 and the Queen's eggs and creep).</summary>
        public static IReadOnlyList<KindInfo> All { get; } = Array.AsReadOnly(Kinds);

        public static KindInfo Of(DelveKind kind) =>
            Kinds.FirstOrDefault(k => k.Kind == kind) ?? throw new ArgumentException(kind + " has no regrow catalog entry.", nameof(kind));

        static readonly Lazy<Dictionary<string, (string Whole, KindInfo Kind)>> byPrefab = new Lazy<Dictionary<string, (string, KindInfo)>>(() =>
        {
            var map = new Dictionary<string, (string, KindInfo)>(StringComparer.Ordinal);
            foreach (var k in Kinds)
                foreach (var w in k.Whole)
                {
                    map[w] = (w, k);
                    foreach (var suffix in k.DamagedSuffixes) map[w + suffix] = (w, k);
                }
            return map;
        });

        /// <summary>The whole prefab of a tracked prefab (itself, or the whole form of a damaged one); null when untracked.</summary>
        public static string? WholeOf(string prefab) => prefab != null && byPrefab.Value.TryGetValue(prefab, out var e) ? e.Whole : null;

        public static DelveKind? KindOf(string prefab) => prefab != null && byPrefab.Value.TryGetValue(prefab, out var e) ? e.Kind.Kind : (DelveKind?)null;

        /// <summary>How a tracked prefab comes back.</summary>
        public static RestoreStyle StyleOf(string prefab)
        {
            if (prefab == null || !byPrefab.Value.TryGetValue(prefab, out var e)) throw new ArgumentException(prefab + " is not tracked by Delve.", nameof(prefab));
            return e.Kind.UnpickPrefabs.Contains(e.Whole) ? RestoreStyle.Unpick : RestoreStyle.Respawn;
        }
    }
}
