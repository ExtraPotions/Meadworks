// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Loot/LootEngine.cs, Loot/DropRoller.cs, Loot/StarDrops.cs and Loot/GildedLoot.cs (order of the steps)
// at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Wildblood namespace; Unity-free over item names and plain rows; the Amount mode
// (CreatureManager, AmountScaling.cs); worthless copies (design E5); the three drop multiplier cheats replace ECR's global
// and boss multipliers and run in every mode; the trophy cheat also covers held boss trophies. Rule-file overrides, extras
// and curated lists arrive with the rule file (B02b).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Loot
{
    /// <summary>One row of a creature's own drop table (vanilla <c>CharacterDrop.Drop</c>, read by the plugin).</summary>
    public sealed class LootRow
    {
        public LootRow(string item, float chance, int amountMin, int amountMax, bool trophy = false, bool onePerPlayer = false, bool levelMultiplier = true)
        {
            if (string.IsNullOrEmpty(item)) throw new ArgumentException("Expected an item.", nameof(item));
            Item = item;
            Chance = chance;
            AmountMin = amountMin;
            AmountMax = Math.Max(amountMin, amountMax);
            Trophy = trophy;
            OnePerPlayer = onePerPlayer;
            LevelMultiplier = levelMultiplier;
        }

        public string Item { get; }

        /// <summary>0 to 1, as the game's table holds it.</summary>
        public float Chance { get; }
        public int AmountMin { get; }

        /// <summary>Inclusive.</summary>
        public int AmountMax { get; }
        public bool Trophy { get; }
        public bool OnePerPlayer { get; }
        public bool LevelMultiplier { get; }
    }

    /// <summary>An item and an amount in the drop list.</summary>
    public readonly struct Drop
    {
        public Drop(string item, int amount)
        {
            Item = item;
            Amount = amount;
        }

        public string Item { get; }
        public int Amount { get; }

        public override string ToString() => Item + " x" + Amount;
    }

    /// <summary>Everything the engine needs to know about one kill, read on the dying creature's owner.</summary>
    public sealed class KillContext
    {
        public KillContext(LootMode mode, int stars)
        {
            Mode = mode;
            Stars = Math.Max(0, stars);
        }

        public LootMode Mode { get; }
        public int Stars { get; }
        public bool IsBoss { get; set; }

        /// <summary>The creature keeps a level Wildblood did not give it (Keep Existing): no Wildblood star loot.</summary>
        public bool KeepsLevel { get; set; }

        /// <summary>Scaled: the star <c>drops</c> line at this star count (creature's own, else its biome's).</summary>
        public float DropsLine { get; set; } = 1f;

        public IReadOnlyList<float> ExtraRollChance { get; set; } = new[] { 0f, 100f };
        public int MaxExtraRolls { get; set; } = 5;
        public int AmountPerStar { get; set; } = 50;
        public int AmountPerStarBosses { get; set; } = 50;
        public bool MultiplyTrophies { get; set; }

        /// <summary>The boss's aspect loot multiplier (bosses only).</summary>
        public float AspectLoot { get; set; } = 1f;

        /// <summary>Gilded's loot multiplier (3 by default) when the creature is Gilded; 1 otherwise.</summary>
        public float GildedLoot { get; set; } = 1f;

        /// <summary>Gilded's purse (Coins x20 by default), scaled by nothing.</summary>
        public Drop? GildedBonus { get; set; }

        /// <summary>Phantom husks, Cloven copies, Enforcer minions, raised skeletons: no drops at all.</summary>
        public bool Worthless { get; set; }

        /// <summary>A boss's held trophy count (<c>BossTrophies.Count</c>); 0 when not a boss.</summary>
        public int BossTrophyCount { get; set; }

        public DropMultipliers Multipliers { get; set; } = DropMultipliers.None;

        /// <summary>Players counted for one-per-player rows on rerolls.</summary>
        public int Players { get; set; } = 1;
    }

    /// <summary>
    /// Applies the loot mode to the drop list the game generated at level 1 (design E1 to E8). Rows of the creature's own
    /// table are Wildblood's to change; anything else in the list (other mods' drops) passes through untouched.
    /// </summary>
    public static class LootEngine
    {
        public static List<Drop> Apply(IEnumerable<Drop> generated, IReadOnlyList<LootRow> table, KillContext ctx, IDice dice)
        {
            if (generated == null) throw new ArgumentNullException(nameof(generated));
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            var result = generated.Where(d => d.Amount > 0).ToList();
            if (ctx.Worthless) return new List<Drop>();
            var own = table.GroupBy(r => r.Item, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            bool Trophy(string item) => own.TryGetValue(item, out var row) && row.Trophy;
            bool OnePerPlayer(string item) => own.TryGetValue(item, out var row) && row.OnePerPlayer;
            bool Follows(string item) => own.ContainsKey(item) && !OnePerPlayer(item) && (ctx.MultiplyTrophies || !Trophy(item));

            var heads = new List<string>();
            if (ctx.IsBoss && ctx.BossTrophyCount > 0)
            {
                heads = table.Where(r => r.Trophy).Select(r => r.Item).Distinct().ToList();
                result.RemoveAll(d => heads.Contains(d.Item));
            }

            var starLoot = !ctx.IsBoss && !ctx.KeepsLevel;
            switch (ctx.Mode)
            {
                case LootMode.Scaled:
                    if (starLoot) Map(result, d => Follows(d.Item) ? LootMath.Scaled(d.Amount, ctx.DropsLine) : d.Amount);
                    break;
                case LootMode.Rolled:
                    if (starLoot) Reroll(result, table, ctx, heads, LootMath.ExtraRolls(ctx.ExtraRollChance, ctx.MaxExtraRolls, ctx.Stars, dice), dice);
                    break;
                case LootMode.Amount:
                    if (!ctx.KeepsLevel)
                    {
                        var percent = ctx.IsBoss ? ctx.AmountPerStarBosses : ctx.AmountPerStar;
                        Map(result, d => own.TryGetValue(d.Item, out var row) && row.LevelMultiplier && !row.Trophy && !row.OnePerPlayer
                            ? AmountScaling.CalculateAmount(d.Amount, ctx.Stars + 1, percent, dice) : d.Amount);
                    }
                    break;
                case LootMode.Curated:
                    result.RemoveAll(d => Follows(d.Item));
                    break;
            }

            var factor = ctx.GildedLoot * ctx.Multipliers.Creature * (ctx.IsBoss ? ctx.AspectLoot * ctx.Multipliers.Boss : 1f);
            Map(result, d => !own.ContainsKey(d.Item) || OnePerPlayer(d.Item) ? d.Amount
                : Trophy(d.Item) ? LootMath.Scaled(d.Amount, ctx.Multipliers.Trophy)
                : LootMath.Scaled(d.Amount, factor));

            if (ctx.GildedBonus.HasValue) Add(result, ctx.GildedBonus.Value.Item, ctx.GildedBonus.Value.Amount);
            foreach (var head in heads) Add(result, head, LootMath.Scaled(ctx.BossTrophyCount, ctx.Multipliers.Trophy));
            result.RemoveAll(d => d.Amount <= 0);
            return result;
        }

        private static void Reroll(List<Drop> result, IReadOnlyList<LootRow> table, KillContext ctx, List<string> heads, int rolls, IDice dice)
        {
            for (var roll = 0; roll < rolls; roll++)
                foreach (var row in table)
                {
                    if (heads.Contains(row.Item) || row.OnePerPlayer || (!ctx.MultiplyTrophies && row.Trophy)) continue;
                    Add(result, row.Item, RollRow(row, dice));
                }
        }

        /// <summary>One fresh roll of a table row at level 1: the chance, then an inclusive amount; 0 when the chance fails.</summary>
        public static int RollRow(LootRow row, IDice dice)
        {
            if (dice.Value() > row.Chance) return 0;
            return Math.Min(LootMath.AmountCap, Math.Max(0, dice.Range(row.AmountMin, row.AmountMax + 1)));
        }

        private static void Map(List<Drop> result, Func<Drop, int> amount)
        {
            for (var i = 0; i < result.Count; i++) result[i] = new Drop(result[i].Item, amount(result[i]));
        }

        private static void Add(List<Drop> result, string item, int amount)
        {
            if (amount <= 0) return;
            for (var i = 0; i < result.Count; i++)
                if (result[i].Item == item)
                {
                    result[i] = new Drop(item, Math.Min(LootMath.AmountCap, result[i].Amount + amount));
                    return;
                }
            result.Add(new Drop(item, Math.Min(LootMath.AmountCap, amount)));
        }
    }
}
