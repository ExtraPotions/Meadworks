// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Traits/Aspect.cs, Traits/AspectCatalog.cs, Rules/AspectRules.cs and Rules/AspectDefaults.cs at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; aspect power fields stay with the native engines (B08b, B09); the portal
// attack list (Aspects/PortalAttacks.cs: the Elder and Bonemass) inlined as a prefab set.
// Changed by expDARE, 2026: Kall Fimbulbringer summons (FrozenKing, FrozenKing_p3).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Bosses
{
    /// <summary>
    /// The boss aspects (design D1). The numeric value is what a boss's and an altar's ZDO store, so it is fixed - append
    /// new aspects, never renumber. <see cref="None"/> is the plain fight and a real outcome of every roll.
    /// </summary>
    public enum Aspect
    {
        None = 0,
        Reflective = 1,
        Shielded = 2,
        Mending = 3,
        Summoner = 4,
        Elementalist = 5,
        Enraged = 6,
        Twin = 7,
        Phantom = 8,
        Adaptive = 9,
        Fixated = 10,
        Stormbound = 11,
        Gravitic = 12,
        Colossal = 13,
        Tethered = 14,
        Bountiful = 15,
        Portalbound = 16,
        Nightfall = 17,
        Brutal = 18,
        Echoing = 19,
    }

    public static class Aspects
    {
        /// <summary>The nineteen aspects, None excluded.</summary>
        public static IReadOnlyList<Aspect> InOrder { get; } =
            Array.AsReadOnly(((Aspect[])Enum.GetValues(typeof(Aspect))).Where(a => a != Aspect.None).ToArray());

        /// <summary>Every outcome of a roll, None first.</summary>
        public static IReadOnlyList<Aspect> Outcomes { get; } = Array.AsReadOnly((Aspect[])Enum.GetValues(typeof(Aspect)));

        public static int Bit(Aspect aspect) => 1 << (int)aspect;

        public static IReadOnlyList<Aspect> Of(int mask) => Outcomes.Where(a => (mask & Bit(a)) != 0).ToList();

        public static string Id(Aspect aspect) => aspect.ToString().ToLowerInvariant();
    }

    /// <summary>What one boss summons and which aspects it may roll (null = every aspect).</summary>
    public sealed class BossAspectRule
    {
        public List<string> Summons { get; } = new List<string>();
        public List<Aspect>? Rotation { get; set; }
    }

    /// <summary>The aspect block of the rules: weights, loot multipliers, per-boss rules and Bountiful's extras.</summary>
    public sealed class AspectRules
    {
        private static readonly HashSet<string> PortalAttackBosses = new HashSet<string>(StringComparer.Ordinal) { "gd_king", "Bonemass" };

        public bool Enabled { get; set; } = true;

        /// <summary>Seconds between altar shifts (design D2, setting Altar Shift Seconds).</summary>
        public float ShiftSeconds { get; set; } = 15f;

        public Dictionary<Aspect, float> Chances { get; } = new Dictionary<Aspect, float>();
        public Dictionary<Aspect, float> Loot { get; } = new Dictionary<Aspect, float>();
        public Dictionary<string, BossAspectRule> Bosses { get; } = new Dictionary<string, BossAspectRule>(StringComparer.Ordinal);

        /// <summary>Bountiful's <c>extra aspects</c> field.</summary>
        public int BountifulExtras { get; set; } = 2;

        public float ChanceOf(Aspect aspect) => Chances.TryGetValue(aspect, out var v) ? v : 0f;

        public float LootOf(Aspect aspect) => Loot.TryGetValue(aspect, out var v) && v > 0f ? v : 1f;

        public bool InRotation(string bossPrefab, Aspect aspect)
        {
            Bosses.TryGetValue(bossPrefab ?? "", out var rule);
            if (aspect == Aspect.Summoner && (rule == null || rule.Summons.Count == 0)) return false;
            if (aspect == Aspect.Portalbound && !PortalAttackBosses.Contains(bossPrefab ?? "")) return false;
            return aspect == Aspect.None || rule?.Rotation == null || rule.Rotation.Contains(aspect);
        }

        /// <summary>EliteCreaturesReborn's defaults: none 42, each 10; loot gentlest first; vanilla summons; the kraken's narrowed rotation.</summary>
        public static AspectRules Default()
        {
            var rules = new AspectRules();
            rules.Chances[Aspect.None] = 42f;
            foreach (var aspect in Aspects.InOrder) rules.Chances[aspect] = 10f;
            foreach (var pair in LootTable) rules.Loot[pair.Key] = pair.Value;
            foreach (var pair in SummonTable)
            {
                var rule = new BossAspectRule();
                rule.Summons.AddRange(pair.Value);
                rules.Bosses[pair.Key] = rule;
            }
            foreach (var pair in LeftOutTable)
                rules.Bosses[pair.Key] = new BossAspectRule { Rotation = Aspects.InOrder.Where(a => Array.IndexOf(pair.Value, a) < 0).ToList() };
            return rules;
        }

        // Gentlest first. Twin pays 1.0 per boss because both twins drop full loot: the fight already pays double.
        private static readonly Dictionary<Aspect, float> LootTable = new Dictionary<Aspect, float>
        {
            [Aspect.None] = 1f, [Aspect.Twin] = 1f, [Aspect.Shielded] = 1.1f, [Aspect.Enraged] = 1.2f,
            [Aspect.Elementalist] = 1.2f, [Aspect.Mending] = 1.3f, [Aspect.Phantom] = 1.3f,
            [Aspect.Reflective] = 1.4f, [Aspect.Summoner] = 1.5f,
            [Aspect.Stormbound] = 1.2f, [Aspect.Colossal] = 1.2f, [Aspect.Adaptive] = 1.3f, [Aspect.Fixated] = 1.3f,
            [Aspect.Gravitic] = 1.3f, [Aspect.Tethered] = 1f, [Aspect.Bountiful] = 2f, [Aspect.Portalbound] = 1.2f,
            [Aspect.Nightfall] = 1.3f, [Aspect.Brutal] = 1.2f, [Aspect.Echoing] = 1.3f,
        };

        // Each vanilla boss calls creatures of its own biome, by prefab name.
        private static readonly Dictionary<string, string[]> SummonTable = new Dictionary<string, string[]>
        {
            ["Eikthyr"] = new[] { "Boar", "Neck" },
            ["gd_king"] = new[] { "Greydwarf_Elite", "Greydwarf_Shaman" },
            ["Bonemass"] = new[] { "Draugr_Elite", "BlobElite" },
            ["Dragon"] = new[] { "Hatchling" },
            ["GoblinKing"] = new[] { "GoblinBrute", "GoblinShaman" },
            ["SeekerQueen"] = new[] { "SeekerBrute", "Seeker" },
            ["Fader"] = new[] { "Charred_Melee", "Charred_Archer" },
            // Kall Fimbulbringer calls the Jotun army (Krigen, Hexen). Each phase is its own prefab and rolls on its own.
            ["FrozenKing"] = new[] { "JotunWarrior", "JotunWitch" },
            ["FrozenKing_p3"] = new[] { "JotunWarrior", "JotunWitch" },
        };

        // Bosses from other products whose fight an aspect would break (Bestiary registers its own through
        // meadworks.creatures.rules v1, design 9.2; this is the EliteCreaturesReborn default for the same kraken).
        private static readonly Dictionary<string, Aspect[]> LeftOutTable = new Dictionary<string, Aspect[]>
        {
            ["ECP_Kraken"] = new[] { Aspect.Twin, Aspect.Phantom, Aspect.Tethered, Aspect.Brutal, Aspect.Echoing },
        };
    }
}
