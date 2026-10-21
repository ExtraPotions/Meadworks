// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// The most another peer accepts from a hit or a player's ZDO for the item-local combat values it applies (the
    /// target's owner: penetration, paralysis length), from the running, server-synced rules: the largest cap among the
    /// channels of the effect, in the applied unit. 0 when the rules
    /// define no inscription on it, so a value the server does not allow is never applied. Refreshed once per rules
    /// generation.
    /// </summary>
    internal static class CombatCaps
    {
        private static int _generation = -1;
        private static float _penetration;
        private static float _paralyzeSeconds;

        /// <summary>Share of a resistance a hit may bypass (never more than all of it).</summary>
        public static float Penetration
        {
            get
            {
                Refresh();
                return _penetration;
            }
        }

        public static float ParalyzeSeconds
        {
            get
            {
                Refresh();
                return _paralyzeSeconds;
            }
        }

        private static void Refresh()
        {
            ChannelPlan plan = ChannelPlan.Current;
            if (plan.Generation == _generation)
            {
                return;
            }
            _penetration = Math.Min(1f, CapOf(plan, EffectKind.Penetration));
            _paralyzeSeconds = CapOf(plan, EffectKind.Paralyze);
            _generation = plan.Generation;
        }

        private static float CapOf(ChannelPlan plan, EffectKind kind)
        {
            float largest = 0f;
            for (int c = 0; c < plan.Count; c++)
            {
                if (plan.Kinds[c] == kind)
                {
                    ChannelDef channel = plan.Channels[c];
                    largest = Math.Max(largest, AggregateBuilder.Scale(channel, channel.Cap));
                }
            }
            return largest;
        }
    }
}
