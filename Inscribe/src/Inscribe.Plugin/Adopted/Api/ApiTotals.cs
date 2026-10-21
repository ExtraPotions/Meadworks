// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Adopted.Loot;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// <c>GetPlayerTotal</c> and <c>GetItemTotal</c> (api.md section 3), in the inscriptions' own unit (percent points or
    /// flat numbers). A player total is the capped sum of every player-global channel of the effect and param: for the
    /// local player read from the last effects rebuild (the channels this peer sums anyway, external effects included);
    /// for another player the value it publishes on its ZDO, for the
    /// effects that publish one (the loot-find effects and the shared stats), else 0. An item total sums the item's own
    /// active inscriptions of the effect, each channel capped on the item. Both are 0 while inscription effects are
    /// switched off. Allocation-free after the first call per rules generation, for callers that read every frame.
    /// </summary>
    internal static class ApiTotals
    {
        // Keyed on (effect, param) as a tuple, so a call with a param builds no key string.
        private static readonly Dictionary<(string Effect, string? Param), int[]> ChannelCache = new Dictionary<(string Effect, string? Param), int[]>();
        private static ChannelPlan? _cachePlan;

        public static float Player(Player? player, string? effect, string? param)
        {
            if (player == null || string.IsNullOrEmpty(effect) || !ItemEffects.Enabled)
            {
                return 0f;
            }
            return ReferenceEquals(player, global::Player.m_localPlayer) ? Local(effect!, param) : Published(player, effect!, param);
        }

        public static float Item(ItemDrop.ItemData? item, string? effect, string? param)
        {
            if (item == null || string.IsNullOrEmpty(effect) || !ItemEffects.Enabled)
            {
                return 0f;
            }
            IReadOnlyList<EffectRoll> rolls = ItemState.Read(item).EffectRolls;
            IReadOnlyList<ChannelDef> channels = ActiveRules.Current.Affixes.Channels;
            float total = 0f;
            for (int i = 0; i < rolls.Count; i++)
            {
                int c = rolls[i].Def.ChannelIndex;
                if (Matches(rolls[i].Def, effect!, param) && FirstOfChannel(rolls, i) && c >= 0 && c < channels.Count)
                {
                    total += Math.Min(ChannelSum(rolls, c), channels[c].Cap);
                }
            }
            return total;
        }

        private static float Local(string effect, string? param)
        {
            ChannelPlan? plan = AggregateBuilder.LastPlan;
            float[] sums = AggregateBuilder.Sums;
            float total = 0f;
            foreach (int c in plan == null ? Array.Empty<int>() : ChannelsOf(plan, effect, param))
            {
                if (c < sums.Length && !EffectKinds.IsItemLocal(plan!.Kinds[c]))
                {
                    total += plan.Clamp(c, sums[c]);
                }
            }
            return total;
        }

        // Only the published, param-less totals: the loot-find stats (in percent points) and the shared stats (in the
        // game's unit: a fraction for a percent effect, turned back into percent points here).
        private static float Published(Player player, string effect, string? param)
        {
            ZDO? zdo = param == null && player.m_nview != null ? player.m_nview.GetZDO() : null;
            if (zdo == null)
            {
                return 0f;
            }
            int find = FindKeys.StatOf(effect);
            if (find >= 0)
            {
                return FindChannels.Current.Clamp(find, zdo.GetFloat(FindKeys.Hashes[find], 0f));
            }
            int stat = PlayerStats.StatOf(EffectKinds.Of(effect));
            return stat < 0 ? 0f : PlayerStats.Of(player, stat) * (IsPercent(effect) ? 100f : 1f);
        }

        private static bool IsPercent(string effect)
        {
            foreach (ChannelDef channel in ActiveRules.Current.Affixes.Channels)
            {
                if (channel.Effect.Id == effect)
                {
                    return channel.Sample.Value == AffixValueType.Percent;
                }
            }
            return false;
        }

        private static int[] ChannelsOf(ChannelPlan plan, string effect, string? param)
        {
            if (!ReferenceEquals(plan, _cachePlan))
            {
                ChannelCache.Clear();
                _cachePlan = plan;
            }
            (string, string?) key = (effect, param);
            if (!ChannelCache.TryGetValue(key, out int[] found))
            {
                List<int> list = new List<int>();
                for (int c = 0; c < plan.Count; c++)
                {
                    if (plan.Channels[c].Effect.Id == effect && plan.Channels[c].Param == param)
                    {
                        list.Add(c);
                    }
                }
                found = list.ToArray();
                ChannelCache[key] = found;
            }
            return found;
        }

        private static bool Matches(AffixDef def, string effect, string? param) => def.Effect == effect && def.Param == param;

        private static bool FirstOfChannel(IReadOnlyList<EffectRoll> rolls, int index)
        {
            for (int j = 0; j < index; j++)
            {
                if (rolls[j].Def.ChannelIndex == rolls[index].Def.ChannelIndex)
                {
                    return false;
                }
            }
            return true;
        }

        private static float ChannelSum(IReadOnlyList<EffectRoll> rolls, int channel)
        {
            float sum = 0f;
            for (int i = 0; i < rolls.Count; i++)
            {
                if (rolls[i].Def.ChannelIndex == channel)
                {
                    sum += rolls[i].Roll.Value;
                }
            }
            return sum;
        }
    }
}
