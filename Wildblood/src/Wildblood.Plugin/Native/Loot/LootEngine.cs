// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Loot
{
    /// <summary>
    /// Reworks a kill's drop list by the loot rules (`loot.md`): the mode decides quantities, the creature's own
    /// `creatures:` rules override and extend them, and the global (or boss, or Gilded) multiplier scales the result. Runs
    /// inside the GenerateDropList postfix, on the dying creature's owner, so every roll here happens exactly once
    /// and every player sees the one pile the game replicates. Trophies step outside all of it unless the trophy
    /// switch says otherwise - except a row the file names explicitly, which is the server's own words and honoured.
    /// A boss's own trophies stand outside it whatever the switch, in every mode: held out first, paid back last as
    /// one per player near it plus one per star plus one (<see cref="BossTrophies"/>). The engine only touches rows it owns: the creature's own
    /// table and rows the rule file names. A row another mod injected into the same list (EpicLoot's materials, say)
    /// passes through untouched by mode, strip and multiplier alike, whatever order Harmony happens to run the
    /// postfixes in.
    /// </summary>
    internal static class LootEngine
    {
        private sealed class Context
        {
            public LootRules Loot = null!;
            public CreatureLootRule? Rule;
            public int Stars;
            public bool IsBoss;
            public bool KeepsNativeLevel;
            public bool IndependentLoot;
            public float AspectMultiplier = 1f;
            public float GildedMultiplier = 1f;
            public float DropsMultiplier;
            public int ExtraRolls;
            public readonly HashSet<string> Overridden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>The prefabs this engine may touch: the creature's own table plus rows the file names.
            /// Anything else in the list was put there by another mod and passes through untouched.</summary>
            public readonly HashSet<GameObject> Ours = new HashSet<GameObject>();

            /// <summary>True when trophies follow the mode; false keeps them exactly as the game rolled them.</summary>
            public bool TrophiesFollow;

            /// <summary>A boss's own trophies, out of the list while the rules run and paid back last.</summary>
            public BossTrophies Held = BossTrophies.None;
        }

        public static void Rework(CharacterDrop drop, EliteController controller, List<KeyValuePair<GameObject, int>> result)
        {
            LootRules loot = RuleState.Active.Loot;
            if (loot.Mode == LootMode.Vanilla)
            {
                ReworkVanilla(drop, controller, result);
                return;
            }
            Context ctx = Build(loot, drop, controller);
            ctx.Held = BossTrophies.Hold(drop, controller, result, ctx.Overridden);
            StripOverridden(ctx, result);
            if (loot.Mode == LootMode.Curated)
            {
                StripBase(ctx, result);
            }
            else
            {
                ApplyMode(ctx, drop, result);
                RollOverrides(ctx, result);
            }
            RollExtras(ctx, result);
            Multiply(ctx, result);
            ctx.Held.Pay(result); // a boss's heads, likewise
            ApplyCheats(drop, ctx.Ours, ctx.IsBoss, result);
            GildedLoot.AddBonus(controller, result); // Gilded's purse, after every multiplier and scaled by none
            BossMaterialRewards.Append(controller, result);
        }

        /// <summary>Vanilla: the rules stand aside, and only what pays with them off touches the list - a boss aspect,
        /// Gilded, and a boss's trophies. No `creatures:` entry applies here, so none can claim a trophy.</summary>
        private static void ReworkVanilla(CharacterDrop drop, EliteController controller,
            List<KeyValuePair<GameObject, int>> result)
        {
            BossTrophies held = BossTrophies.Hold(drop, controller, result, overridden: null);
            AspectLoot.ApplyAlone(drop, controller, result); // a boss aspect pays even with the loot rules off
            GildedLoot.ApplyMultiplier(drop, controller, result);
            held.Pay(result);
            var ours = new HashSet<GameObject>();
            foreach (var row in drop.m_drops) if (row.m_prefab != null) ours.Add(row.m_prefab);
            ApplyCheats(drop, ours, controller.Creature != null && controller.Creature.IsBoss(), result);
            GildedLoot.AddBonus(controller, result);
            BossMaterialRewards.Append(controller, result);
        }

        // Existing portable multiplier semantics, evaluated at use time against the actual C02 authority.
        private static void ApplyCheats(CharacterDrop drop, HashSet<GameObject> ours, bool boss, List<KeyValuePair<GameObject, int>> result)
        {
            var multipliers = ExtraPotions.Wildblood.Runtime.Plugin.Settings?.Multipliers
                ?? ExtraPotions.Wildblood.Loot.DropMultipliers.None;
            if (multipliers.IsNone) return;
            var protectedRows = new HashSet<GameObject>();
            foreach (var row in drop.m_drops)
                if (row.m_prefab != null && row.m_onePerPlayer) protectedRows.Add(row.m_prefab);
            for (int i = result.Count - 1; i >= 0; i--)
            {
                var pair = result[i];
                if (!ours.Contains(pair.Key) || protectedRows.Contains(pair.Key)) continue;
                var factor = DropRoller.IsTrophy(pair.Key) ? multipliers.Trophy
                    : multipliers.Creature * (boss ? multipliers.Boss : 1f);
                var count = ExtraPotions.Wildblood.Loot.LootMath.Scaled(pair.Value, factor);
                if (count == 0) result.RemoveAt(i);
                else result[i] = new KeyValuePair<GameObject, int>(pair.Key, count);
            }
        }

        private static Context Build(LootRules loot, CharacterDrop drop, EliteController controller)
        {
            Context ctx = new Context
            {
                Loot = loot,
                Rule = FindRule(controller),
                Stars = controller.Traits.Stars,
                IsBoss = controller.Creature != null && controller.Creature.IsBoss(),
                KeepsNativeLevel = !controller.LootOnly && controller.KeepsNativeLevel,
                IndependentLoot = controller.LootOnly,
                AspectMultiplier = AspectLoot.Factor(controller),
                GildedMultiplier = GildedLoot.Factor(controller),
            };
            ctx.TrophiesFollow = ctx.Rule?.MultiplyTrophies ?? loot.MultiplyTrophies;
            ctx.DropsMultiplier = StarDrops.Multiplier(ctx.Rule, controller, ctx.IsBoss, ctx.Stars);
            ctx.ExtraRolls = StarDrops.ExtraRolls(loot, ctx.IsBoss, ctx.Stars, ctx.KeepsNativeLevel, ctx.IndependentLoot);
            if (ctx.Rule != null)
            {
                foreach (DropRule row in ctx.Rule.Overrides)
                {
                    ctx.Overridden.Add(row.Item);
                }
            }
            ClaimTable(ctx, drop);
            return ctx;
        }

        /// <summary>The creature's own table rows are ours to rework; rows the file names join in RollRow.</summary>
        private static void ClaimTable(Context ctx, CharacterDrop drop)
        {
            foreach (CharacterDrop.Drop row in drop.m_drops)
            {
                if (row.m_prefab != null)
                {
                    ctx.Ours.Add(row.m_prefab);
                }
            }
        }

        private static CreatureLootRule? FindRule(EliteController controller)
        {
            string name = Utils.GetPrefabName(controller.gameObject);
            return RuleState.Active.CreatureLoot.TryGetValue(name, out CreatureLootRule rule) ? rule : null;
        }

        /// <summary>Rows the file overrides lose their game-rolled contribution; the file's version replaces it.</summary>
        private static void StripOverridden(Context ctx, List<KeyValuePair<GameObject, int>> result)
        {
            if (ctx.Overridden.Count > 0)
            {
                result.RemoveAll(pair => ctx.Overridden.Contains(pair.Key.name));
            }
        }

        /// <summary>Curated: the creature's own table is ignored entirely; protected trophies survive, and so
        /// does anything another mod put in the list - Curated replaces our table, not theirs.</summary>
        private static void StripBase(Context ctx, List<KeyValuePair<GameObject, int>> result)
        {
            result.RemoveAll(pair => ctx.Ours.Contains(pair.Key)
                && (ctx.TrophiesFollow || !DropRoller.IsTrophy(pair.Key)));
        }

        private static void ApplyMode(Context ctx, CharacterDrop drop, List<KeyValuePair<GameObject, int>> result)
        {
            if (ctx.Loot.Mode == LootMode.Scaled)
            {
                ScaleRows(ctx, result);
            }
            else if (ctx.Loot.Mode == LootMode.Rolled)
            {
                RerollTable(ctx, drop, result);
            }
            else if (ctx.Loot.Mode == LootMode.Amount && !ctx.KeepsNativeLevel && (ctx.IndependentLoot || !RuleState.Active.KeepsLevel(ctx.Stars, ctx.IsBoss)))
            {
                var percent = ctx.IsBoss ? ctx.Loot.AmountPerStarBosses : ctx.Loot.AmountPerStar;
                for (int i = 0; i < result.Count; i++)
                {
                    var pair = result[i];
                    var row = drop.m_drops.Find(candidate => candidate.m_prefab == pair.Key);
                    if (row == null || !ExtraPotions.Wildblood.Loot.AmountScaling.ScalesQuantity(row.m_levelMultiplier, row.m_levelMultiplierNotAmount) ||
                        row.m_onePerPlayer || DropRoller.IsTrophy(pair.Key)) continue;
                    result[i] = new KeyValuePair<GameObject, int>(pair.Key,
                        ExtraPotions.Wildblood.Loot.AmountScaling.CalculateAmount(pair.Value, ctx.Stars + 1, percent,
                            ExtraPotions.Wildblood.Runtime.UnityDice.Instance));
                }
            }
        }

        private static void ScaleRows(Context ctx, List<KeyValuePair<GameObject, int>> result)
        {
            if (Mathf.Approximately(ctx.DropsMultiplier, 1f))
            {
                return;
            }
            for (int i = 0; i < result.Count; i++)
            {
                if (ctx.Ours.Contains(result[i].Key) && (ctx.TrophiesFollow || !DropRoller.IsTrophy(result[i].Key)))
                {
                    result[i] = new KeyValuePair<GameObject, int>(
                        result[i].Key, DropRoller.Scaled(result[i].Value, ctx.DropsMultiplier));
                }
            }
        }

        /// <summary>Rolled: the whole table again per earned extra roll, each row independent, exactly like the
        /// game's own base roll - which is what makes a rare drop's chance genuinely repeat.</summary>
        private static void RerollTable(Context ctx, CharacterDrop drop, List<KeyValuePair<GameObject, int>> result)
        {
            for (int roll = 0; roll < ctx.ExtraRolls; roll++)
            {
                foreach (CharacterDrop.Drop row in drop.m_drops)
                {
                    if (row.m_prefab == null || ctx.Overridden.Contains(row.m_prefab.name)
                        || ctx.Held.Holds(row.m_prefab) || (!ctx.TrophiesFollow && DropRoller.IsTrophy(row.m_prefab)))
                    {
                        continue;
                    }
                    DropRoller.Add(result, row.m_prefab, DropRoller.RollGameRow(row));
                }
            }
        }

        private static void RollOverrides(Context ctx, List<KeyValuePair<GameObject, int>> result)
        {
            if (ctx.Rule == null)
            {
                return;
            }
            foreach (DropRule row in ctx.Rule.Overrides)
            {
                if (!row.Remove)
                {
                    RollRow(ctx, row, followsMode: true, result);
                }
            }
        }

        private static void RollExtras(Context ctx, List<KeyValuePair<GameObject, int>> result)
        {
            if (ctx.Rule == null)
            {
                return;
            }
            foreach (DropRule row in ctx.Rule.Extras)
            {
                RollRow(ctx, row, row.PerStar, result);
            }
        }

        /// <summary>A file row: one base roll, plus the mode's treatment when it follows the mode - extra rolls
        /// under Rolled and Curated, the quantity line under Scaled.</summary>
        private static void RollRow(Context ctx, DropRule row, bool followsMode, List<KeyValuePair<GameObject, int>> result)
        {
            GameObject? prefab = DropRoller.FindPrefab(row.Item);
            if (prefab == null)
            {
                return;
            }
            ctx.Ours.Add(prefab);
            int amount = DropRoller.RollRuleRow(row);
            if (followsMode && ctx.Loot.Mode == LootMode.Amount)
            {
                if (!ctx.KeepsNativeLevel && (ctx.IndependentLoot || !RuleState.Active.KeepsLevel(ctx.Stars, ctx.IsBoss)) && !DropRoller.IsTrophy(prefab))
                    amount = ExtraPotions.Wildblood.Loot.AmountScaling.CalculateAmount(amount, ctx.Stars + 1,
                        ctx.IsBoss ? ctx.Loot.AmountPerStarBosses : ctx.Loot.AmountPerStar,
                        ExtraPotions.Wildblood.Runtime.UnityDice.Instance);
            }
            else if (followsMode && ctx.Loot.Mode != LootMode.Scaled)
            {
                for (int roll = 0; roll < ctx.ExtraRolls; roll++)
                {
                    amount += DropRoller.RollRuleRow(row);
                }
            }
            else if (followsMode && amount > 0)
            {
                amount = DropRoller.Scaled(amount, ctx.DropsMultiplier);
            }
            DropRoller.Add(result, prefab, amount);
        }

        /// <summary>The world-wide multiplier, after everything else; a boss takes the boss and aspect factors on top, a
        /// Gilded creature its <c>loot</c> factor.</summary>
        private static void Multiply(Context ctx, List<KeyValuePair<GameObject, int>> result)
        {
            float factor = ctx.Loot.GlobalMultiplier * ctx.GildedMultiplier
                * (ctx.IsBoss ? ctx.Loot.BossMultiplier * ctx.AspectMultiplier : 1f);
            if (Mathf.Approximately(factor, 1f))
            {
                return;
            }
            for (int i = 0; i < result.Count; i++)
            {
                if (ctx.Ours.Contains(result[i].Key) && (ctx.TrophiesFollow || !DropRoller.IsTrophy(result[i].Key)))
                {
                    result[i] = new KeyValuePair<GameObject, int>(
                        result[i].Key, DropRoller.Scaled(result[i].Value, factor));
                }
            }
        }
    }
}
