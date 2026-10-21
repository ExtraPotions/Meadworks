// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// <c>GetPlayerInscriptionsJson</c> (api.md section 3): what the local player's gear inscribes, for another mod's
    /// stat list (PackPanel's stat sheet). Every active inscription on the items that count
    /// (<see cref="ItemEffects.CollectLocal"/>: the game's equipment and the equipment providers' items), one entry per
    /// inscription id in the order first met, its values summed and worded as the tooltip words one line with that value
    /// (<see cref="AffixLines.Sentence"/>), with each item's own value for a breakdown. <c>capped</c>: a player-global
    /// channel whose inscriptions together (this one and any other feeding it) pass its cap, so the game applies less
    /// than the sum. Another player, or effects switched off: an empty list. Allocates: for a panel that reads a few times
    /// a second, not for every frame.
    /// </summary>
    internal static class ApiPlayerLines
    {
        private sealed class Entry
        {
            public AffixDef Def = null!;
            public float Total;
            public readonly List<ActiveAffix> Sources = new List<ActiveAffix>();
        }

        private static readonly List<ActiveAffix> Active = new List<ActiveAffix>();
        private static readonly List<ItemDrop.ItemData> Scratch = new List<ItemDrop.ItemData>();

        public static string? Json(Player? player)
        {
            if (player == null)
            {
                return null;
            }
            Active.Clear();
            if (ReferenceEquals(player, Player.m_localPlayer))
            {
                ItemEffects.CollectLocal(Active, Scratch);
            }
            List<Entry> entries = Group(Active);
            float[] channelSums = ChannelSums(Active);
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < entries.Count; i++)
            {
                Append(sb.Append(i > 0 ? "," : ""), entries[i], channelSums);
            }
            return sb.Append(']').ToString();
        }

        private static List<Entry> Group(List<ActiveAffix> active)
        {
            List<Entry> entries = new List<Entry>();
            foreach (ActiveAffix affix in active)
            {
                Entry? entry = entries.Find(e => e.Def.Id == affix.Def.Id);
                if (entry == null)
                {
                    entry = new Entry { Def = affix.Def };
                    entries.Add(entry);
                }
                entry.Total += affix.Roll.Value;
                entry.Sources.Add(affix);
            }
            return entries;
        }

        /// <summary>Every channel's sum over all the player's active inscriptions (the cap is on the channel, not the inscription).</summary>
        private static float[] ChannelSums(List<ActiveAffix> active)
        {
            float[] sums = new float[ActiveRules.Current.Affixes.Channels.Count];
            foreach (ActiveAffix affix in active)
            {
                if (affix.Channel >= 0 && affix.Channel < sums.Length)
                {
                    sums[affix.Channel] += affix.Roll.Value;
                }
            }
            return sums;
        }

        private static void Append(StringBuilder sb, Entry entry, float[] channelSums)
        {
            AffixDef def = entry.Def;
            bool itemLocal = EffectKinds.IsItemLocal(EffectKinds.Of(def.Effect));
            sb.Append("{\"id\":").Append(ApiItems.Quote(def.Id))
                .Append(",\"category\":").Append(ApiItems.Quote(EnumIds<AffixCategory>.Id(def.Category)))
                .Append(",\"line\":").Append(ApiItems.Quote(AffixLines.Sentence(def.Id, entry.Total, def)))
                .Append(",\"total\":").Append(Numbers.Format(entry.Total))
                .Append(",\"capped\":").Append(!itemLocal && Capped(def, channelSums) ? "true" : "false")
                .Append(",\"item_local\":").Append(itemLocal ? "true" : "false")
                .Append(",\"sources\":[");
            for (int i = 0; i < entry.Sources.Count; i++)
            {
                AppendSource(sb.Append(i > 0 ? ",{" : "{"), entry.Sources[i]);
            }
            sb.Append("]}");
        }

        private static bool Capped(AffixDef def, float[] channelSums)
        {
            IReadOnlyList<ChannelDef> channels = ActiveRules.Current.Affixes.Channels;
            int c = def.ChannelIndex;
            return c >= 0 && c < channelSums.Length && c < channels.Count && channelSums[c] > channels[c].Cap;
        }

        private static void AppendSource(StringBuilder sb, ActiveAffix source)
        {
            string value = source.Def.Value == AffixValueType.Flag ? "" : DisplayWords.Signed(source.Roll.Value, source.Def);
            sb.Append("\"item\":").Append(ApiItems.Quote(Words.Localize(source.Item.m_shared.m_name)))
                .Append(",\"value\":").Append(ApiItems.Quote(value)).Append('}');
        }
    }
}
