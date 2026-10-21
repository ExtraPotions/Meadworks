// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; GemName shared with meadworks.items.sockets.
using System.Text;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// The socket lines of the tooltip block (sockets.md section 6), after the inscriptions, one per socket in order: a
    /// gem in its own colour ("Thor's Gem: +6 lightning damage  T5", greyed and marked while its inscription is dormant),
    /// an unreadable entry greyed, then "Empty socket" for each free one. Built once per item state and detail level.
    /// </summary>
    internal static class GemLines
    {
        public static void Append(StringBuilder sb, ItemState state, TooltipDetail detail)
        {
            for (int i = 0; i < state.Gems.Count; i++)
            {
                AppendGem(sb, state, i, detail);
            }
            int unreadable = state.FilledSockets - state.Gems.Count;
            for (int i = 0; i < unreadable + state.FreeSockets; i++)
            {
                string key = i < unreadable ? "$ecf_ui_socket_unreadable" : "$ecf_ui_socket_empty";
                sb.Append('\n').Append(RarityPalette.Grey).Append(Words.Localize(key)).Append(RarityPalette.Close);
            }
        }

        private static void AppendGem(StringBuilder sb, ItemState state, int index, TooltipDetail detail)
        {
            GemRoll gem = state.Gems[index];
            AffixDef? def = state.GemDefinitionAt(index);
            bool dormant = !state.IsGemActiveAt(index);
            sb.Append('\n').Append(dormant ? RarityPalette.Grey : RarityPalette.OpenTag(StoneVisuals.Tint(gem.GemId)));
            sb.Append(Words.Localize("$ecf_ui_socket_gem", GemName(gem.GemId), AffixLines.Sentence(gem.Roll.Id, gem.Roll.Value, def, brief: detail != TooltipDetail.Full)));
            if (detail != TooltipDetail.Compact)
            {
                string tier = def != null ? def.ShownTier(gem.Roll.Tier).ToString() : "?";
                sb.Append("  ").Append(Words.Localize("$ecf_ui_tier", tier));
            }
            if (dormant)
            {
                sb.Append("  ").Append(Words.Localize("$ecf_ui_dormant"));
            }
            sb.Append(RarityPalette.Close);
        }

        internal static string GemName(string gemId)
        {
            StoneDef? stone = ActiveRules.Current.Economy.Stone(gemId);
            return DisplayWords.Name(stone?.Name ?? "$ecf_stone_" + gemId, gemId);
        }
    }
}
