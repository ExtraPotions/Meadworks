// Larder - Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// GrindstoneSkills/GrindstoneSkills/Skills/Book/SkillPage.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: content only, shared CS1 lines, no private skill book or renderer.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    public sealed class SkillPage
    {
        private readonly List<SkillPageLine> entries = new List<SkillPageLine>();
        public SkillPage(Player player, float level) { Player = player; Level = level; }
        public Player Player { get; }
        public float Level { get; }
        public float Factor => Mathf.Clamp01(Level / 100f);
        public string About { get; set; } = "";
        public void Heading(string text) => entries.Add(new SkillPageLine(text, true));
        public void Line(string text) => entries.Add(new SkillPageLine(text));
        public void Line(string text, string term, string tip) => entries.Add(new SkillPageLine(text, false, term, tip));
        public void Perk(string name, float level, string tip)
        {
            if (level <= CookLevel.MaxLevel) entries.Add(new SkillPageLine(name, false, name, tip));
        }
        public IReadOnlyList<SkillPageLine> Lines()
        {
            var result = new List<SkillPageLine> { new SkillPageLine(About) };
            result.AddRange(entries); return result;
        }
        public static string Percent(float share) => $"{share * 100f:0.#}%";
        public static string Number(float value) => value.ToString("0.#");
    }
}
