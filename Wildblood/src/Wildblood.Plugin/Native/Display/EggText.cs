// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Runtime.CompilerServices;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Adopted.Breeding;
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// The line an egg shows about what it will hatch - "Hatches with 2 stars, Leeching" - on its hover text on the
    /// ground and on its tooltip in an inventory. Without it an egg's inheritance would be invisible until it hatched.
    /// The mutation word takes its star colour, the same palette the nameplate uses. Both are asked for every frame, so
    /// the line is kept beside the egg and drawn again only when its stored traits or the palette change.
    /// </summary>
    internal static class EggText
    {
        private sealed class Memo
        {
            public string Raw = "";
            public int Palette = -1, Language = -1;
            public string? Line;
        }

        private static readonly ConditionalWeakTable<ItemDrop.ItemData, Memo> Kept = new ConditionalWeakTable<ItemDrop.ItemData, Memo>();

        /// <summary>The line for an egg carrying traits from this mod, or null for any other item.</summary>
        public static string? For(ItemDrop.ItemData item)
        {
            if (item == null || !item.m_customData.TryGetValue(TraitKeys.EggTraits, out string raw))
            {
                return null;
            }
            Memo memo = Kept.GetOrCreateValue(item);
            if (memo.Raw != raw || memo.Palette != PaletteSettings.Version || memo.Language != WildbloodText.LanguageVersion)
            {
                memo.Raw = raw;
                memo.Palette = PaletteSettings.Version;
                memo.Language = WildbloodText.LanguageVersion;
                memo.Line = Draw(item);
            }
            return memo.Line;
        }

        private static string? Draw(ItemDrop.ItemData item)
        {
            Lineage.Newborn? born = EggData.Read(item);
            if (born == null)
            {
                return null;
            }
            CreatureTraits traits = born.Traits;
            if (traits.Stars == 0 && !traits.Any)
            {
                return WildbloodText.Text("egg_plain");
            }
            string stars = traits.Stars == 1 ? WildbloodText.Text("egg_one_star") : WildbloodText.Format("egg_stars", traits.Stars);
            string text = WildbloodText.Format("egg_hatches", stars);
            foreach (Mutation mutation in traits.Active())
            {
                text += $", <color=#{ColorUtility.ToHtmlStringRGB(PaletteSettings.Of(mutation))}>{WildbloodText.Text("mutation_" + mutation.ToString().ToLowerInvariant())}</color>";
            }
            return text;
        }
    }
}
