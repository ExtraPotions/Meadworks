// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Text;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Builds the tooltip affix block (display.md section 3), localized, top to bottom: at Full the item's
    /// class and level (classes-and-tiers.md section 9), newer-format notice, affix lines, socket lines, sealed. Lines that do not
    /// apply are left out; an item with nothing to show gets an empty block. Called once per item state and detail
    /// level by <see cref="DisplayCache"/>, never per frame. Runs on the viewing client only.
    /// </summary>
    internal static class TooltipBlock
    {
        // Our own builder: the game's shared tooltip StringBuilder is never touched.
        private static readonly StringBuilder Sb = new StringBuilder(512);

        public static string Build(ItemState state, ItemDrop.ItemData item, TooltipDetail detail, bool showDormant)
        {
            Sb.Clear();
            if ((state.IsMagic || state.Rarity != null) && detail == TooltipDetail.Full)
                Sb.Append('\n').Append(RarityPalette.Grey).Append(ClassLine(item)).Append(RarityPalette.Close);
            if (state.IsNewerFormat)
            {
                Sb.Append('\n').Append(RarityPalette.Grey).Append(Words.Localize("$ecf_ui_newer_format")).Append(RarityPalette.Close);
            }
            AffixLines.Append(Sb, state, detail, showDormant, ItemClasses.Classify(item).DamageScale);
            GemLines.Append(Sb, state, detail);
            AppendSealed(Sb, state);
            // The block follows the vanilla tooltip after one blank line.
            return Sb.Length == 0 ? "" : "\n" + Sb.ToString();
        }

        /// <summary>"Swords · item level 4"; "item level 4" alone for an item without a class.</summary>
        private static string ClassLine(ItemDrop.ItemData item)
        {
            ItemClass? itemClass = ItemClasses.ClassOf(item);
            string level = ItemTier.Of(item).ToString();
            return itemClass == null
                ? Words.Localize("$ecf_ui_item_level", level)
                : Words.Localize("$ecf_ui_class_level", DisplayWords.Name(itemClass.Name, itemClass.Id), level);
        }

        /// <summary>"Sealed" in dark red, whatever sealed the item.</summary>
        private static void AppendSealed(StringBuilder sb, ItemState state)
        {
            if (!state.IsSealed)
            {
                return;
            }
            string text = Words.Localize("$ecf_ui_sealed_generic");
            sb.Append('\n').Append(RarityPalette.SealedRed).Append(text).Append(RarityPalette.Close);
        }
    }
}
