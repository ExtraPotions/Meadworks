// Inscribe - ExtraPotions Meadworks gear inscriptions. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Changed by expDARE, 2026: the SetCreatureLoot payload read with a portable JSON reader instead of YamlDotNet, one
// profile type for boss and creature fields, Inscribe's scroll_rolls and scroll_multiplier added (design 9.2).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCrafting 0.7.0 (Api/ApiLoot.cs keys and rules, Rules/Parsing/
// BossDropParser.cs defaults and ranges), geraldjglasgow/ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: bonus rows take the chisel and the gems, and the removed runes map to their successors as in
// EliteCrafting 0.10.0 (d3b57606cb24775d61b7bda244b22eb89e7e7717); the runes folded on 2026-10-19 (Awakening, Tempering) pay their successor.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Inscribe.Runes;

namespace ExtraPotions.Inscribe.Interop
{
    /// <summary>One guaranteed-chance bonus row: <c>{ rune, chance, amount }</c>.</summary>
    public sealed class LootBonus
    {
        internal LootBonus(string rune, float chance, int amount)
        {
            Rune = rune;
            Chance = chance;
            Amount = amount;
        }

        public string Rune { get; }

        /// <summary>Percent, 0 to 100 (default 100).</summary>
        public float Chance { get; }

        /// <summary>At least 1 (default 1).</summary>
        public int Amount { get; }
    }

    /// <summary>
    /// A modded creature's loot profile (EliteCrafting API <c>SetCreatureLoot</c>, design 9.2): the fields of the economy
    /// YAML's <c>drops.bosses</c> and <c>drops.creatures</c> entries plus <c>prefab</c> (required) and <c>boss</c>, and
    /// Inscribe's scroll fields. Errors refuse the profile; unknown keys and boss-only rolls on a creature are warnings.
    /// </summary>
    public sealed class LootProfile
    {
        public const int Tiers = 8;

        private static readonly string[] Keys =
        {
            "prefab", "boss", "tier", "multiplier", "rune_multiplier", "gear_multiplier", "rune_rolls", "gear_rolls", "bonus", "scroll_rolls", "scroll_multiplier",
        };

        private LootProfile() { }

        public string Prefab { get; private set; } = "";
        public bool Boss { get; private set; }

        /// <summary>1 to 8; a creature's 0 means "from its spawn data or death biome" (boss default 1).</summary>
        public int Tier { get; private set; }

        public float Multiplier { get; private set; } = 1f;
        public float RuneMultiplier { get; private set; } = 1f;
        public float GearMultiplier { get; private set; } = 1f;
        public float ScrollMultiplier { get; private set; } = 1f;

        /// <summary>Boss rolls; a creature's are ignored with a warning.</summary>
        public int RuneRolls { get; private set; }

        public int GearRolls { get; private set; }
        public int ScrollRolls { get; private set; }
        public IReadOnlyList<LootBonus> Bonus { get; private set; } = Array.Empty<LootBonus>();

        /// <summary>The profile, or null with <paramref name="errors"/> listing why not.</summary>
        public static LootProfile? Parse(string? json, out IReadOnlyList<string> errors, out IReadOnlyList<string> warnings)
        {
            var e = new List<string>();
            var w = new List<string>();
            errors = e;
            warnings = w;
            if (string.IsNullOrWhiteSpace(json))
            {
                e.Add("no definition given");
                return null;
            }
            object? root;
            try { root = MiniJson.Parse(json!); }
            catch (FormatException error)
            {
                e.Add("the definition does not parse: " + error.Message);
                return null;
            }
            if (!(root is Dictionary<string, object?> map))
            {
                e.Add("the definition should be a JSON object");
                return null;
            }
            foreach (var key in map.Keys.Where(k => Array.IndexOf(Keys, k) < 0)) w.Add(key + ": unknown key, ignored");
            var p = new LootProfile();
            if (!(Get(map, "prefab") is string prefab) || prefab.Length == 0)
            {
                e.Add("prefab: is required: the creature's prefab name");
                return null;
            }
            p.Prefab = prefab;
            p.Boss = Bool(map, "boss", false, e);
            p.Tier = Int(map, "tier", p.Boss ? 1 : 0, 1, Tiers, e);
            p.Multiplier = Float(map, "multiplier", 1f, 0f, float.MaxValue, e);
            p.RuneMultiplier = Float(map, "rune_multiplier", 1f, 0f, float.MaxValue, e);
            p.GearMultiplier = Float(map, "gear_multiplier", 1f, 0f, float.MaxValue, e);
            p.ScrollMultiplier = Float(map, "scroll_multiplier", 1f, 0f, float.MaxValue, e);
            var runeRolls = Int(map, "rune_rolls", 0, 0, int.MaxValue, e);
            var gearRolls = Int(map, "gear_rolls", 0, 0, int.MaxValue, e);
            p.ScrollRolls = Int(map, "scroll_rolls", 0, 0, int.MaxValue, e);
            if (p.Boss)
            {
                p.RuneRolls = runeRolls;
                p.GearRolls = gearRolls;
            }
            else if (map.ContainsKey("rune_rolls") || map.ContainsKey("gear_rolls"))
            {
                w.Add("rune_rolls: rune_rolls and gear_rolls are for bosses (\"boss\": true); ignored");
            }
            p.Bonus = Bonuses(map, e, w);
            return e.Count == 0 ? p : null;
        }

        private static IReadOnlyList<LootBonus> Bonuses(Dictionary<string, object?> map, List<string> e, List<string> w)
        {
            var node = Get(map, "bonus");
            if (node == null) return Array.Empty<LootBonus>();
            if (!(node is List<object?> rows))
            {
                e.Add("bonus: should be a list of { rune, chance, amount }");
                return Array.Empty<LootBonus>();
            }
            var result = new List<LootBonus>();
            foreach (var item in rows)
            {
                if (!(item is Dictionary<string, object?> row))
                {
                    e.Add("bonus: a bonus row looks like { rune: ascension, chance: 100, amount: 1 }");
                    continue;
                }
                foreach (var key in row.Keys.Where(k => k != "rune" && k != "chance" && k != "amount")) w.Add("bonus." + key + ": unknown key, ignored");
                var rune = Get(row, "rune") as string;
                // EliteCrafting 0.8.0 and 0.9.0 retired the Shaping and Consecrated Runes; a profile naming them pays the successor.
                if (rune != null && Successors.TryGetValue(rune, out var successor))
                {
                    w.Add("bonus: '" + rune + "' was removed in EliteCrafting " + (rune == "shaping" ? "0.8.0" : "0.9.0") + "; paying '" + successor + "' instead");
                    rune = successor;
                }
                else if (LegacyRunes.SuccessorOf(rune) is { } folded)
                {
                    w.Add("bonus: '" + rune + "' was folded into '" + folded + "'; paying '" + folded + "' instead");
                    rune = folded;
                }
                if (rune == null || (RuneCatalog.Default.Find(rune) == null && !SocketStones.IsSocketStone(rune)))
                {
                    e.Add("bonus: '" + rune + "' is not a rune, chisel or gem id (" + string.Join(", ", RuneCatalog.Default.Runes.Select(r => r.Id)) + ", " + SocketStones.ChiselId + ", gem_*)");
                    continue;
                }
                result.Add(new LootBonus(rune, Float(row, "chance", 100f, 0f, 100f, e), Int(row, "amount", 1, 1, int.MaxValue, e)));
            }
            return result;
        }

        private static readonly Dictionary<string, string> Successors = new Dictionary<string, string>(StringComparer.Ordinal) { ["shaping"] = "recasting", ["consecrated"] = "ascension" };

        private static object? Get(Dictionary<string, object?> map, string key) => map.TryGetValue(key, out var v) ? v : null;

        private static bool Bool(Dictionary<string, object?> map, string key, bool fallback, List<string> e)
        {
            if (!map.ContainsKey(key)) return fallback;
            if (map[key] is bool b) return b;
            e.Add(key + ": should be true or false");
            return fallback;
        }

        private static int Int(Dictionary<string, object?> map, string key, int fallback, int min, int max, List<string> e)
        {
            if (!map.ContainsKey(key)) return fallback;
            if (map[key] is double d && d == Math.Floor(d) && d >= min && d <= max) return (int)d;
            e.Add(key + ": should be a whole number from " + min + (max == int.MaxValue ? " up" : " to " + max));
            return fallback;
        }

        private static float Float(Dictionary<string, object?> map, string key, float fallback, float min, float max, List<string> e)
        {
            if (!map.ContainsKey(key)) return fallback;
            if (map[key] is double d && d >= min && d <= max) return (float)d;
            e.Add(key + ": should be a number from " + min + (max == float.MaxValue ? " up" : " to " + max));
            return fallback;
        }
    }
}
