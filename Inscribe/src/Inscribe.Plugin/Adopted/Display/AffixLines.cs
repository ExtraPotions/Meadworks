// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Text;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// The affix lines of the tooltip block (display.md section 3, classes-and-tiers.md section 9): the active prefixes,
    /// then the active suffixes, each group in stored order, then the dormant ones greyed, then (Full only) unreadable
    /// segments. Each line's tier is counted down within its own inscription's ladder. Built once per item state and
    /// detail level.
    /// </summary>
    internal static class AffixLines
    {
        /// <summary><paramref name="scale"/>: the item class's <c>damage_scale</c>, for the ranges of scaled inscriptions.</summary>
        public static void Append(StringBuilder sb, ItemState state, TooltipDetail detail, bool showDormant, float scale)
        {
            AppendActive(sb, state, detail, AffixKind.Prefix, scale);
            AppendActive(sb, state, detail, AffixKind.Suffix, scale);
            for (int i = 0; showDormant && i < state.AffixCount; i++)
            {
                if (!state.IsActiveAt(i))
                {
                    AppendLine(sb, state, i, detail, dormant: true, scale);
                }
            }
            for (int i = 0; detail == TooltipDetail.Full && i < state.Unreadable.Count; i++)
            {
                sb.Append('\n').Append(RarityPalette.Grey)
                    .Append(Words.Localize("$ecf_ui_unreadable", DisplayWords.Verbatim(state.Unreadable[i])))
                    .Append(RarityPalette.Close);
            }
        }

        private static void AppendActive(StringBuilder sb, ItemState state, TooltipDetail detail, AffixKind kind, float scale)
        {
            for (int i = 0; i < state.AffixCount; i++)
            {
                if (state.IsActiveAt(i) && state.DefinitionAt(i)!.Kind == kind)
                {
                    AppendLine(sb, state, i, detail, dormant: false, scale);
                }
            }
        }

        private static void AppendLine(StringBuilder sb, ItemState state, int index, TooltipDetail detail, bool dormant, float scale)
        {
            AffixRoll roll = state.Affixes[index];
            AffixDef? def = state.DefinitionAt(index);
            sb.Append('\n');
            if (dormant)
            {
                sb.Append(RarityPalette.Grey);
            }
            sb.Append(Sentence(roll.Id, roll.Value, def, brief: detail != TooltipDetail.Full));
            if (detail == TooltipDetail.Full)
            {
                AppendRange(sb, roll, def, scale);
            }
            AppendMarkers(sb, roll, def, detail, dormant);
        }

        /// <summary>Tier (not at Compact; "?" for an orphan, whose ladder is unknown) and the dormant word (closing the grey).</summary>
        private static void AppendMarkers(StringBuilder sb, AffixRoll roll, AffixDef? def, TooltipDetail detail, bool dormant)
        {
            if (detail != TooltipDetail.Compact)
            {
                string tier = def != null ? def.ShownTier(roll.Tier).ToString() : "?";
                sb.Append("  ").Append(Words.Localize("$ecf_ui_tier", tier));
            }
            if (dormant)
            {
                sb.Append("  ").Append(Words.Localize("$ecf_ui_dormant")).Append(RarityPalette.Close);
            }
        }

        /// <summary>
        /// The affix's own sentence (<c>$ecf_affix_&lt;id&gt;_line</c>) when a translation has one; otherwise the
        /// generic "value name" line. An orphaned affix (no definition) shows its bare stored value and its name or id.
        /// </summary>

        /// <summary>
        /// The line an inscription of this id shows with this value; also the API's summed lines
        /// (<c>GetPlayerInscriptionsJson</c>), which word a player's total the way the tooltip words one item.
        /// </summary>
        internal static string Sentence(string id, float value, AffixDef? def, bool brief = false)
        {
            // Tooltips at Compact and Standard detail take the short line (user 2026-10-07: "Just say Chain Lighting 5%
            // T8 ... just say 7% knockback T8"); Full detail, the API and the table's texts keep the whole sentence.
            string shortKey = "ecf_affix_" + id + "_short";
            if (brief && DisplayWords.Has(shortKey))
            {
                return Words.Localize("$" + shortKey, DisplayWords.Plain(value));
            }
            string lineKey = "ecf_affix_" + id + "_line";
            if (DisplayWords.Has(lineKey))
            {
                return Words.Localize("$" + lineKey, DisplayWords.Plain(value));
            }
            string name = def != null
                ? DisplayWords.Name(def.Name, id)
                : DisplayWords.Name("$ecf_affix_" + id, id);
            if (def != null && def.Value == AffixValueType.Flag)
            {
                return name;
            }
            string shown = def != null ? DisplayWords.Signed(value, def) : DisplayWords.Plain(value);
            return Words.Localize("$ecf_ui_affix_line", shown, name);
        }

        /// <summary>
        /// Full detail: the stored tier's roll range, when the definition still has that tier; a scaled inscription's
        /// range times the class's <c>damage_scale</c>, as its value was rolled.
        /// </summary>
        private static void AppendRange(StringBuilder sb, AffixRoll roll, AffixDef? def, float scale)
        {
            AffixTierDef? row = def?.TierRow(roll.Tier);
            if (row == null || def!.Value == AffixValueType.Flag)
            {
                return;
            }
            float min = def.Scaled ? Rolling.RollMath.Scale(row.Min, scale, row.Decimals) : row.Min;
            float max = def.Scaled ? Rolling.RollMath.Scale(row.Max, scale, row.Decimals) : row.Max;
            sb.Append(' ').Append(Words.Localize("$ecf_ui_range", DisplayWords.Plain(min), DisplayWords.Plain(max)));
        }
    }
}
