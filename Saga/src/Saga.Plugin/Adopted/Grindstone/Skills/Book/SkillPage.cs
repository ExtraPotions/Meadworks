// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Book/SkillPage.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// One skill's page in the skills panel's info pane (<see cref="SkillBook"/>): a line about the skill, what it does
    /// for the player at their level with the numbers, and its named perks, each described when its name is hovered. A
    /// writer listed in <see cref="SkillPages"/> fills it for the local player as the page opens; <see cref="PageText"/>
    /// turns it into the pane's text. Values are those of that moment, from the synced settings, as the features use them.
    /// </summary>
    public sealed class SkillPage
    {
        internal SkillPage(Player player, global::Skills.SkillType type, string about)
        {
            Player = player;
            Type = type;
            Level = ExtraPotions.Meadworks.Crucible.MeadworksSkills.EffectiveLevel(player, type);
            About = about;
        }

        public Player Player { get; }
        public global::Skills.SkillType Type { get; }

        /// <summary>The player's level, bonuses included (whole numbers, as the game floors it): the level the game and the features use.</summary>
        public float Level { get; }

        /// <summary>The level as a share of 100, 0..1: the game's skill factor.</summary>
        public float Factor => Mathf.Clamp01(Level / 100f);

        /// <summary>One short sentence: what the skill is for and how it is trained. Starts as the game's description.</summary>
        public string About { get; set; }

        internal List<PageEntry> Entries { get; } = new List<PageEntry>();
        internal List<PagePerk> Perks { get; } = new List<PagePerk>();

        /// <summary>A small heading over the lines after it ("Stars", "The fight"). Only for a page long enough to need groups.</summary>
        public void Heading(string text) => Entries.Add(new PageEntry(text, "", "", true));

        /// <summary>One thing the skill does at this level, short, with its number: "Max health +3.3".</summary>
        public void Line(string text) => Entries.Add(new PageEntry(text, "", "", false));

        /// <summary>A line with one word explained: hovering <paramref name="term"/>, which appears in the text, shows the tip.</summary>
        public void Line(string text, string term, string tip) => Entries.Add(new PageEntry(text, term, tip, false));

        /// <summary>
        /// A named perk, unlocked at a level (0: from the start). A level above 100 means the perk is turned off, and it
        /// is not listed. Hovering its name shows the tip, which says what it does with its numbers.
        /// </summary>
        public void Perk(string name, float level, string tip)
        {
            if (level <= 100f)
                Perks.Add(new PagePerk(name, level, tip));
        }

        /// <summary>A share as a percent with at most one decimal: 0.125 is "12.5%".</summary>
        public static string Percent(float share) => $"{share * 100f:0.#}%";

        /// <summary>Two shares as a range of whole percents: 0.47 and 0.772 are "47–77%".</summary>
        public static string Range(float low, float high) => $"{low * 100f:0}–{high * 100f:0}%";

        /// <summary>A number with at most one decimal.</summary>
        public static string Number(float value) => value.ToString("0.#");

        /// <summary>A time in the largest unit that keeps it readable: "45 s", "10 min", "1.5 h".</summary>
        public static string Duration(float seconds)
        {
            if (seconds < 120f)
                return $"{Number(seconds)} s";
            return seconds < 7200f ? $"{Number(seconds / 60f)} min" : $"{Number(seconds / 3600f)} h";
        }
    }
}
