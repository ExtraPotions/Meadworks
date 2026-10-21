// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Publishes the local player's loot-find totals to its own player ZDO (effects-runtime.md section 7): the capped sum
    /// of each find channel over the equipped items that count (<see cref="ItemEffects.CollectLocal"/>, so the
    /// <c>Affix effects</c> switch and dormant affixes are respected). Runs on the local player's client only - the one
    /// peer that owns that ZDO - and never on a dedicated server (no local player). After each rebuild of the effects
    /// (<see cref="EffectRuntime.Rebuilt"/>, at most once a frame), so it follows every trigger of the aggregate:
    /// equipment set-up, spawn, a state write to an equipped item, a rules apply, the <c>Affix effects</c> switch, a
    /// counted-gear change and the API's InvalidatePlayer. A value is written only when it differs from what the ZDO
    /// holds, so normal play sends nothing.
    /// </summary>
    internal static class FindPublisher
    {
        private static readonly List<ActiveAffix> Affixes = new List<ActiveAffix>();
        private static readonly List<ItemDrop.ItemData> Scratch = new List<ItemDrop.ItemData>();
        private static readonly float[] Sums = new float[FindKeys.Count];
        private static float[] _channelSums = Array.Empty<float>();

        public static void Install() => EffectRuntime.Rebuilt += Publish;
        internal static void Uninstall() => EffectRuntime.Rebuilt -= Publish;

        /// <summary>Recomputes and writes the local player's totals. Safe to call at any time; no local player = no-op.</summary>
        public static void Publish()
        {
            Player? player = Player.m_localPlayer;
            ZNetView? nview = player != null ? player.m_nview : null;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return;
            }
            Sum();
            ZDO zdo = nview.GetZDO();
            for (int stat = 0; stat < FindKeys.Count; stat++)
            {
                if (zdo.GetFloat(FindKeys.Hashes[stat], 0f) != Sums[stat])
                {
                    zdo.Set(FindKeys.Hashes[stat], Sums[stat]);
                    LogChange(stat);
                }
            }
        }

        private static void Sum()
        {
            Array.Clear(Sums, 0, Sums.Length);
            ItemEffects.CollectLocal(Affixes, Scratch);
            if (Affixes.Count > 0)
            {
                AddChannels(FindChannels.Current, ActiveRules.Current.Affixes.Channels);
            }
            Affixes.Clear();   // hold no item references between events
            Scratch.Clear();
        }

        private static void AddChannels(FindChannels channels, IReadOnlyList<ChannelDef> defs)
        {
            float[] byChannel = ChannelSums(channels);
            for (int channel = 0; channel < byChannel.Length && channel < defs.Count; channel++)
            {
                int stat = channels.StatOf[channel];
                if (stat >= 0 && byChannel[channel] > 0f)
                {
                    Sums[stat] += Math.Min(byChannel[channel], defs[channel].Cap);
                }
            }
        }

        // Caps are on each channel's sum over all equipped items (effects-runtime.md section 5), never per item.
        private static float[] ChannelSums(FindChannels channels)
        {
            if (_channelSums.Length != channels.StatOf.Length)
            {
                _channelSums = new float[channels.StatOf.Length];
            }
            Array.Clear(_channelSums, 0, _channelSums.Length);
            foreach (ActiveAffix affix in Affixes)
            {
                int channel = affix.Channel;
                if (channel >= 0 && channel < _channelSums.Length && channels.StatOf[channel] >= 0)
                {
                    _channelSums[channel] += affix.Roll.Value;
                }
            }
            return _channelSums;
        }

        private static void LogChange(int stat)
        {
            if (ModSettings.LogRolls.Value)
            {
                Log.Info($"loot find (local player): {FindKeys.Keys[stat]} = {Numbers.Format(Sums[stat])}");
            }
        }
    }
}
