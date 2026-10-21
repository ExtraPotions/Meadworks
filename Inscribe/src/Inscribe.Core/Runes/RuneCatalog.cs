// Inscribe - ExtraPotions Meadworks gear inscriptions. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Changed by expDARE, 2026: immutable rune definitions with the seven EliteCrafting 0.7.0 defaults as code (the YAML
// family loader, R02b, will override them), a weighted pick over IDice, and lookups by id and prefab; the MeadWorks Honing
// Rune and the Epic and Legendary tiers in the applies-to lists; Awakening folded into Ascension and Tempering into Honing
// (2026-10-19), their old prefabs kept as legacy items that convert to the successor.
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCrafting 0.7.0 (Rules/Model/StoneDef.cs, Rules/Model/RuleEnums.cs,
// Rolling/RollMath.cs PickWeighted, config/EliteCrafting_economy.yml runes), geraldjglasgow/ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCrafting 0.10.0 (config/EliteCrafting_economy.yml runes: Shaping and Consecrated
// retired, the Serpent Rune sealed; prefab names by Core/Ids.cs Pascal), geraldjglasgow/ValheimMods d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Inscribe.Rarity;

namespace ExtraPotions.Inscribe.Runes
{
    /// <summary>What a rune does: Ascension promotes, Shaping and Consecrated added, Cleansing strips, the Serpent corrupts, Recasting rerolls, Honing hones.</summary>
    public enum RuneVerb { Promote, Add, Strip, Corrupt, Reroll, Hone }

    /// <summary>The Serpent Rune's outcomes; every one seals the item (design B5).</summary>
    public enum SerpentOutcome { SealOnly, AddInscription, ChaoticReroll }

    public readonly struct OutcomeWeight
    {
        public OutcomeWeight(SerpentOutcome outcome, float weight)
        {
            Outcome = outcome;
            Weight = weight;
        }

        public SerpentOutcome Outcome { get; }
        public float Weight { get; }
    }

    /// <summary>One rune (design B1, B7). Players see "rune"; prefab names stay <c>ECF_&lt;Id&gt;</c> so existing stacks load.</summary>
    public sealed class RuneDef
    {
        public RuneDef(string id, RuneVerb verb, IReadOnlyList<string> appliesTo, bool confirm = false, int overflow = 1,
            IReadOnlyList<OutcomeWeight>? outcomes = null, IReadOnlyDictionary<string, int>? cost = null, bool enabled = true, int stack = 50, float itemWeight = 0.2f)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Expected a rune id.", nameof(id));
            Id = id;
            Prefab = PrefabOf(id);
            Verb = verb;
            AppliesTo = appliesTo?.ToArray() ?? Array.Empty<string>();
            Confirm = confirm;
            Overflow = Math.Max(0, overflow);
            Outcomes = outcomes?.ToArray() ?? Array.Empty<OutcomeWeight>();
            Cost = cost ?? new Dictionary<string, int>();
            Enabled = enabled;
            Stack = stack;
            ItemWeight = itemWeight;
        }

        public string Id { get; }

        /// <summary><c>ECF_</c> plus the PascalCase id.</summary>
        public string Prefab { get; }

        /// <summary>The prefab of a rune, chisel or gem id as EliteCrafting 0.10.0 names it: <c>ECF_</c> plus the PascalCase id (<c>dvergr_chisel</c> is <c>ECF_DvergrChisel</c>).</summary>
        public static string PrefabOf(string id) =>
            "ECF_" + string.Concat(id.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));

        public RuneVerb Verb { get; }
        public IReadOnlyList<string> AppliesTo { get; }

        /// <summary>Asks first through the player's confirm mode (Cleansing and Serpent by default).</summary>
        public bool Confirm { get; }

        /// <summary>The Serpent's add may go this many past the rarity's maximum and prefix or suffix limit.</summary>
        public int Overflow { get; }

        public IReadOnlyList<OutcomeWeight> Outcomes { get; }

        /// <summary>Runes consumed per use by rarity; a rarity absent here costs 1.</summary>
        public IReadOnlyDictionary<string, int> Cost { get; }

        public bool Enabled { get; }
        public int Stack { get; }
        public float ItemWeight { get; }

        public bool AppliesToRarity(string rarityId) => AppliesTo.Contains(rarityId);

        public int CostFor(string rarityId) => Cost.TryGetValue(rarityId, out var cost) ? Math.Max(cost, 0) : 1;

        public RuneDef WithEnabled(bool enabled) =>
            new RuneDef(Id, Verb, AppliesTo, Confirm, Overflow, Outcomes, Cost, enabled, Stack, ItemWeight);

        public override string ToString() => Id;
    }

    /// <summary>The fixed rune set (design B1): Ascension, Recasting, Honing, Cleansing and the Serpent (Sealed) Rune; Shaping and Consecrated left in EliteCrafting 0.8.0 and 0.9.0, Awakening and Tempering on 2026-10-19 (<see cref="LegacyRunes"/>).</summary>
    public sealed class RuneCatalog
    {
        private readonly RuneDef[] runes;

        public RuneCatalog(IEnumerable<RuneDef> runes)
        {
            this.runes = runes.ToArray();
            if (this.runes.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != this.runes.Length) throw new ArgumentException("Duplicate rune id.", nameof(runes));
        }

        public static RuneCatalog Default { get; } = new RuneCatalog(new[]
        {
            // One rarity step at any tier below Epic (2026-10-19: Awakening folded in); Legendary stays boss and raid only.
            new RuneDef("ascension", RuneVerb.Promote, new[] { "normal", "magic", "rare" }),
            new RuneDef("recasting", RuneVerb.Reroll, new[] { "magic" }),
            // MeadWorks batch 3: one inscription the player picks a tier up (the native rune pipeline performs it); Tempering folded in.
            new RuneDef("honing", RuneVerb.Hone, new[] { "magic", "rare", RarityTiers.Epic, RarityTiers.Legendary }),
            new RuneDef("cleansing", RuneVerb.Strip, new[] { "magic", "rare", RarityTiers.Epic, RarityTiers.Legendary }, confirm: true),
            new RuneDef("serpent", RuneVerb.Corrupt, new[] { "magic", "rare", RarityTiers.Epic, RarityTiers.Legendary }, confirm: true, overflow: 1, outcomes: new[]
            {
                new OutcomeWeight(SerpentOutcome.SealOnly, 35f), new OutcomeWeight(SerpentOutcome.AddInscription, 35f), new OutcomeWeight(SerpentOutcome.ChaoticReroll, 30f),
            }),
        });

        public IReadOnlyList<RuneDef> Runes => runes;

        public RuneDef? Find(string? id) => id == null ? null : runes.FirstOrDefault(r => r.Id == id);

        public RuneDef? ByPrefab(string? prefab) => prefab == null ? null : runes.FirstOrDefault(r => r.Prefab == prefab);
    }

    /// <summary>
    /// Runes folded into another on 2026-10-19 (user decision): Awakening into Ascension, Tempering into Honing. Their
    /// prefabs stay registered so saved stacks load; every stack found in an inventory, a chest or a Rune Table becomes the
    /// same number of the successor, once. Converting twice changes nothing.
    /// </summary>
    public static class LegacyRunes
    {
        public static readonly IReadOnlyDictionary<string, string> Successors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["awakening"] = "ascension",
            ["tempering"] = "honing",
        };

        public static IEnumerable<string> Ids => Successors.Keys;

        public static bool IsLegacy(string? id) => id != null && Successors.ContainsKey(id);

        public static string? SuccessorOf(string? id) => id != null && Successors.TryGetValue(id, out var next) ? next : null;

        private static readonly Dictionary<string, string> PrefabSuccessors =
            Successors.ToDictionary(p => RuneDef.PrefabOf(p.Key), p => RuneDef.PrefabOf(p.Value), StringComparer.Ordinal);

        /// <summary><c>ECF_Awakening</c> to <c>ECF_Ascension</c>; null for any other prefab.</summary>
        public static string? SuccessorPrefab(string? prefab) =>
            prefab != null && PrefabSuccessors.TryGetValue(prefab, out var next) ? next : null;

        /// <summary>
        /// Moves every legacy count into its successor (a Rune Table's stored runes) and zeroes the legacy count; the
        /// number of runes moved. The total never changes, and a second call moves nothing.
        /// </summary>
        public static int MoveCounts(IDictionary<string, int> counts)
        {
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            var moved = 0;
            foreach (var pair in Successors)
            {
                if (!counts.TryGetValue(pair.Key, out var held) || held <= 0) continue;
                counts.TryGetValue(pair.Value, out var already);
                counts[pair.Value] = already + held;
                counts[pair.Key] = 0;
                moved += held;
            }
            return moved;
        }
    }

    public static class Weighted
    {
        /// <summary>Index of a weighted pick; weights at or below 0 never win; -1 when nothing can win.</summary>
        public static int Pick(IReadOnlyList<float> weights, IDice dice)
        {
            double total = 0;
            for (var i = 0; i < weights.Count; i++) total += Math.Max(weights[i], 0f);
            if (total <= 0) return -1;
            var target = dice.Value() * total;
            var last = -1;
            for (var i = 0; i < weights.Count; i++)
            {
                var weight = Math.Max(weights[i], 0f);
                if (weight <= 0f) continue;
                last = i;
                if (target < weight) return i;
                target -= weight;
            }
            return last;
        }
    }
}
