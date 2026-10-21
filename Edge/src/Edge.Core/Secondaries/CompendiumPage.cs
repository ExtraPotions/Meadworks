// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackCompendiumManager.cs (BuildPageText and LocalizeGroupHeading: one page with the intro, Quickstep, then the
// catalog presets under their group headings, and its colours). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Unity-free, Edge text keys through a resolver, Quickstep only while Edge's quickstep runs,
// no in-text icon markers.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Text;

namespace ExtraPotions.Edge.Secondaries
{
    /// <summary>The single "Secondary Attacks" page Edge adds to the game's Compendium (Texts/Lore).</summary>
    public static class CompendiumPage
    {
        public const string TitleKey = "compendium_title";
        public const string IntroKey = "compendium_intro";
        public const string QuickstepNameKey = "compendium_quickstep_name";
        public const string QuickstepDescriptionKey = "compendium_quickstep_description";

        public static readonly string[] Keys =
        {
            TitleKey, IntroKey, QuickstepNameKey, QuickstepDescriptionKey,
            GroupKey(PresetGroup.Ranged), GroupKey(PresetGroup.Melee), GroupKey(PresetGroup.BloodMagic), GroupKey(PresetGroup.SummonQuality),
        };

        public static string GroupKey(PresetGroup group)
        {
            switch (group)
            {
                case PresetGroup.Ranged: return "compendium_group_ranged";
                case PresetGroup.Melee: return "compendium_group_melee";
                case PresetGroup.BloodMagic: return "compendium_group_blood_magic";
                default: return "compendium_group_summon_quality";
            }
        }

        public static string Text(Func<string, string> text, bool quickstep)
        {
            var builder = new StringBuilder();
            builder.Append("<color=#D6D6D6>").Append(text(IntroKey)).Append("</color>\n\n");
            if (quickstep)
                builder.Append(Heading(text(QuickstepNameKey))).Append(text(QuickstepDescriptionKey)).Append("\n\n\n");
            PresetGroup? previous = null;
            foreach (var entry in PresetCatalog.Entries)
            {
                if (previous != entry.Group)
                {
                    if (previous != null) builder.Append('\n');
                    builder.Append(Heading(text(GroupKey(entry.Group))));
                    previous = entry.Group;
                }
                builder.Append("<color=orange><b>").Append(text(entry.NameKey)).Append("</b></color>\n")
                    .Append(text(entry.DescriptionKey)).Append("\n\n");
            }
            return builder.ToString().TrimEnd();
        }

        static string Heading(string words) => "<color=#FFD27A><b>" + words + "</b></color>\n\n";
    }
}
