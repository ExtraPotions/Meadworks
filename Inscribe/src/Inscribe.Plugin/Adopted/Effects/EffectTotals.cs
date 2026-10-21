// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>One channel of the local player's last rebuild, for <c>ecraft stats</c> (console-commands.md).</summary>
    public readonly struct EffectChannelTotal
    {
        public EffectChannelTotal(ChannelDef channel, float sum, float applied, string sources)
        {
            Channel = channel;
            Sum = sum;
            Applied = applied;
            Sources = sources;
        }

        public ChannelDef Channel { get; }

        /// <summary><c>effect</c> or <c>effect:param</c>.</summary>
        public string Key => Channel.Key;

        /// <summary>Raw sum of the stored values over every counted item (percent points or flat units).</summary>
        public float Sum { get; }

        /// <summary>The cap on the sum; <see cref="float.PositiveInfinity"/> = none.</summary>
        public float Cap => Channel.Cap;

        /// <summary>
        /// What the game gets: the sum clamped to the cap, in the affix's own unit. For an item-local channel the cap
        /// applies per item and each item applies its own value, so this is the plain sum.
        /// </summary>
        public float Applied { get; }

        /// <summary>Item-local (applies to the item it sits on) rather than player-global.</summary>
        public bool ItemLocal => EffectKinds.IsItemLocal(EffectKinds.Of(Channel.Effect.Id));

        /// <summary>The equipment positions that feed it: <c>right</c>, <c>left</c>, <c>head</c>, <c>chest</c>, <c>legs</c>, <c>cape</c>, <c>utility</c>.</summary>
        public string Sources { get; }
    }

    /// <summary>The local player's effect totals as of the last rebuild.</summary>
    public sealed class EffectSnapshot
    {
        internal EffectSnapshot(List<EffectChannelTotal> channels)
        {
            Channels = channels;
        }

        /// <summary>The <c>Affix effects</c> switch.</summary>
        public bool Enabled { get; internal set; }

        public int MagicItemsEquipped { get; internal set; }
        public int ActiveAffixes { get; internal set; }

        /// <summary>Seconds since the last rebuild; negative when there has been none.</summary>
        public float SecondsSinceRebuild { get; internal set; }

        /// <summary>Every channel with a non-zero sum, in channel order (Phase 1 and Phase 2 effects alike).</summary>
        public IReadOnlyList<EffectChannelTotal> Channels { get; }

        /// <summary>
        /// The Phase 2 runtime states in force right now, one short English line each (<c>ward 12 left</c>,
        /// <c>evader's fury</c>, <c>steel rhythm</c>, <c>momentum</c>, <c>on a path</c>, <c>coin stacks 2.5</c>), for
        /// <c>ecraft stats</c>. Empty when none is.
        /// </summary>
        public IReadOnlyList<string> States { get; internal set; } = new List<string>();
    }

    /// <summary>Read side of the effects runtime for commands and diagnostics. Allocates; call on demand only.</summary>
    public static class EffectTotals
    {
        public static EffectSnapshot Snapshot()
        {
            List<EffectChannelTotal> channels = new List<EffectChannelTotal>();
            ChannelPlan? plan = AggregateBuilder.LastPlan;
            if (plan != null)
            {
                Collect(plan, channels);
            }
            float at = AggregateBuilder.RebuiltAt;
            return new EffectSnapshot(channels)
            {
                Enabled = ItemEffects.Enabled,
                MagicItemsEquipped = AggregateBuilder.MagicItemCount,
                ActiveAffixes = AggregateBuilder.ActiveAffixCount,
                SecondsSinceRebuild = at < 0f ? -1f : UnityEngine.Time.time - at,
                States = CurrentStates(),
            };
        }

        private static List<string> CurrentStates()
        {
            List<string> states = new List<string>();
            if (CombatWindows.WardPool > 0f)
            {
                states.Add($"ward {Core.Numbers.Format(CombatWindows.WardPool)} left");
            }
            AddIf(states, CombatWindows.FuryActive, "evader's fury");
            AddIf(states, CombatWindows.RhythmActive, "steel rhythm");
            AddIf(states, CombatWindows.MomentumActive, "momentum");
            AddIf(states, PathGround.OnPath, "on a path");
            AddIf(states, AttackBonuses.CoinStacks > 0f, $"coin stacks {Core.Numbers.Format(AttackBonuses.CoinStacks)}");
            return states;
        }

        private static void AddIf(List<string> into, bool condition, string text)
        {
            if (condition)
            {
                into.Add(text);
            }
        }

        private static void Collect(ChannelPlan plan, List<EffectChannelTotal> into)
        {
            float[] sums = AggregateBuilder.Sums;
            int[] sources = AggregateBuilder.Sources;
            for (int c = 0; c < plan.Count && c < sums.Length; c++)
            {
                if (sums[c] == 0f)
                {
                    continue;
                }
                ChannelDef channel = plan.Channels[c];
                bool itemLocal = EffectKinds.IsItemLocal(plan.Kinds[c]);
                float applied = itemLocal ? sums[c] : plan.Clamp(c, sums[c]);
                into.Add(new EffectChannelTotal(channel, sums[c], applied, EquipPositions.Describe(sources[c])));
            }
        }
    }
}
