// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The player-global totals another peer needs, published on the player's own ZDO (effects-runtime.md section 7,
    /// the pattern of the loot-find keys): the creature's owner reads Dazing Blows and Beast Whisperer, the ship's
    /// owner Fair Winds and Sea Ward, the hit target's owner Lingering Wounds, the resource's owner Deep Vein, Heartwood and Harvester, the dying animal's owner
    /// Butcher's Cut, and every client Hearthlight and Mistbane. Written by the player's own client (it owns its player
    /// ZDO) at the end of a rebuild, only when a value changed, so normal play sends nothing. A reader clamps what it
    /// reads to the running rules' cap (a client cannot
    /// publish more than the server's rules allow), and a player without the key reads 0.
    /// <para>
    /// Beside the floats, the int <c>ecf_wants</c> (<see cref="WantsKillRestore"/>, <see cref="WantsDodgeFury"/>): which
    /// of the argument-less routed RPCs this player's client acts on, so their senders (the creature's owner at a kill,
    /// the attacker's peer at a dodged melee hit) send one only to a player who has the inscription. A player without
    /// the key wants nothing.
    /// </para>
    /// </summary>
    internal static class PlayerStats
    {
        public const int Daze = 0, Light = 1, Demist = 2, Taming = 3, Sail = 4, YieldMining = 5, YieldLumber = 6, Harvest = 7,
            Dot = 8, Butcher = 9, ShipWard = 10;

        /// <summary>Bits of <c>ecf_wants</c>: Reaper / Soul Reaper (<see cref="KillCredit"/>), Evader's Fury (<see cref="MeleeDodge"/>).</summary>
        public const int WantsKillRestore = 1, WantsDodgeFury = 2;

        private static readonly int WantsHash = "ecf_wants".GetStableHashCode();

        private static readonly EffectKind[] Kinds =
        {
            EffectKind.StaggerDurationDealt, EffectKind.LightAura, EffectKind.DemistRadius, EffectKind.TamingSpeed,
            EffectKind.SailSpeed, EffectKind.YieldMining, EffectKind.YieldLumber, EffectKind.YieldPickable,
            EffectKind.DotDuration, EffectKind.ButcherYield, EffectKind.ShipDamageTaken,
        };

        private static readonly int[] Hashes =
        {
            "ecf_daze".GetStableHashCode(), "ecf_light".GetStableHashCode(), "ecf_demist".GetStableHashCode(),
            "ecf_taming".GetStableHashCode(), "ecf_sail".GetStableHashCode(), "ecf_yield_mining".GetStableHashCode(),
            "ecf_yield_lumber".GetStableHashCode(), "ecf_harvest".GetStableHashCode(),
            "ecf_dot".GetStableHashCode(), "ecf_butcher".GetStableHashCode(), "ecf_ship_ward".GetStableHashCode(),
        };

        private static readonly float[] Caps = new float[Kinds.Length];
        private static int _capsGeneration = -1;

        /// <summary>End of a rebuild on the local player's client.</summary>
        public static void Publish(Player player)
        {
            ZDO? zdo = player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner() ? player.m_nview.GetZDO() : null;
            if (zdo == null)
            {
                return;
            }
            AggregateValues v = AggregateBuilder.Values;
            for (int i = 0; i < Kinds.Length; i++)
            {
                float value = ItemEffects.Enabled ? v[Kinds[i]] : 0f;
                if (zdo.GetFloat(Hashes[i], 0f) != value)
                {
                    zdo.Set(Hashes[i], value);
                }
            }
            PublishWants(zdo);
        }

        private static void PublishWants(ZDO zdo)
        {
            int wants = ItemEffects.Enabled ? WantedBits(AggregateBuilder.Values) : 0;
            if (zdo.GetInt(WantsHash, 0) != wants)
            {
                zdo.Set(WantsHash, wants);
            }
        }

        private static int WantedBits(AggregateValues v)
        {
            float[] restore = v.KillRestore;
            bool kill = restore[AggregateValues.Health] > 0f || restore[AggregateValues.Stamina] > 0f || restore[AggregateValues.Eitr] > 0f;
            return (kill ? WantsKillRestore : 0) | (v[EffectKind.DodgeFury] > 0f ? WantsDodgeFury : 0);
        }

        /// <summary>Whether the player whose ZDO this is acts on the routed RPC <paramref name="bit"/> (read by its sender).</summary>
        public static bool Wants(ZDO zdo, int bit) => (zdo.GetInt(WantsHash, 0) & bit) != 0;

        /// <summary>The stat a kind is published as, or -1 (the API reads other players' totals through it).</summary>
        public static int StatOf(EffectKind kind) => Array.IndexOf(Kinds, kind);

        /// <summary>A player's published total, clamped to the running rules.</summary>
        public static float Of(Player? player, int stat)
        {
            ZDO? zdo = player != null && player.m_nview != null ? player.m_nview.GetZDO() : null;
            return zdo == null ? 0f : Clamp(stat, zdo.GetFloat(Hashes[stat], 0f));
        }

        /// <summary>The published total of the hit's attacker (0 for a hit without one or from a non-player).</summary>
        public static float OfAttacker(HitData hit, int stat)
        {
            if (hit.m_attacker.IsNone() || ZDOMan.instance == null)
            {
                return 0f;
            }
            return OfZdo(ZDOMan.instance.GetZDO(hit.m_attacker), stat);
        }

        /// <summary>The published total in a player ZDO already looked up (0 for null or a non-player's).</summary>
        public static float OfZdo(ZDO? zdo, int stat) => zdo == null ? 0f : Clamp(stat, zdo.GetFloat(Hashes[stat], 0f));

        private static float Clamp(int stat, float value)
        {
            RefreshCaps();
            return float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(value, Caps[stat]));
        }

        // Per rules generation: the largest cap (in the published unit) among the channels feeding each stat; 0 when the
        // running rules have no affix on it, so a stat the server does not define reads as 0 whatever a ZDO says.
        private static void RefreshCaps()
        {
            ChannelPlan plan = ChannelPlan.Current;
            if (plan.Generation == _capsGeneration)
            {
                return;
            }
            Array.Clear(Caps, 0, Caps.Length);
            for (int c = 0; c < plan.Count; c++)
            {
                int stat = Array.IndexOf(Kinds, plan.Kinds[c]);
                ChannelDef channel = plan.Channels[c];
                if (stat >= 0)
                {
                    Caps[stat] = Math.Max(Caps[stat], AggregateBuilder.Scale(channel, channel.Cap));
                }
            }
            _capsGeneration = plan.Generation;
        }
    }
}
