// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Totals of <see cref="LootRoller.Simulate"/>: how many kills, how many of each stone and each gear rarity. Console
    /// output is English and not localized (DECISIONS LOC-3).
    /// </summary>
    public sealed class LootSimulation
    {
        private readonly SortedDictionary<string, int> _stones = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, int> _rarities = new Dictionary<string, int>(System.StringComparer.Ordinal);

        internal LootSimulation(int tier, int stars, string? creature)
        {
            Tier = tier;
            Stars = stars;
            Creature = creature;
        }

        public int Tier { get; }
        public int Stars { get; }
        public string? Creature { get; }
        public int Kills { get; private set; }
        public int StoneCount { get; private set; }
        public int GearCount { get; private set; }
        public IReadOnlyDictionary<string, int> Stones => _stones;
        public IReadOnlyDictionary<string, int> Rarities => _rarities;

        internal void Add(LootPlan plan)
        {
            Kills++;
            StoneCount += plan.Stones.Count;
            GearCount += plan.Gear.Count;
            foreach (StoneDef stone in plan.Stones)
            {
                _stones.TryGetValue(stone.Id, out int n);
                _stones[stone.Id] = n + 1;
            }
            foreach (RarityDef rarity in plan.Gear)
            {
                _rarities.TryGetValue(rarity.Id, out int n);
                _rarities[rarity.Id] = n + 1;
            }
        }

        public override string ToString()
        {
            StringBuilder text = new StringBuilder();
            text.Append($"{Kills} kills, tier {Tier}, stars {Stars}{(Creature != null ? ", as " + Creature : "")}: ");
            text.Append($"{StoneCount} runes ({PerKill(StoneCount)}/kill), {GearCount} gear ({PerKill(GearCount)}/kill)\n");
            AppendCounts(text, "runes", _stones, StoneCount);
            AppendCounts(text, "gear", _rarities, GearCount);
            return text.ToString();
        }

        private string PerKill(int count) =>
            (Kills == 0 ? 0.0 : (double)count / Kills).ToString("0.####", CultureInfo.InvariantCulture);

        private static void AppendCounts(StringBuilder text, string label, IEnumerable<KeyValuePair<string, int>> counts, int total)
        {
            text.Append("  ").Append(label).Append(':');
            foreach (KeyValuePair<string, int> pair in counts)
            {
                double share = total == 0 ? 0.0 : 100.0 * pair.Value / total;
                text.Append(' ').Append(pair.Key).Append(' ').Append(pair.Value)
                    .Append(" (").Append(share.ToString("0.#", CultureInfo.InvariantCulture)).Append("%)");
            }
            text.Append('\n');
        }
    }
}
