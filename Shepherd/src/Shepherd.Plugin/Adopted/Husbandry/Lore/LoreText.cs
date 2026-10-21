// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Lore/LoreText.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Shepherd C05 duration units, local detail and literal replicated names.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Runtime.Integration;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    public static class LoreText
    {
        public static bool Visible => HusbandrySkill.Active && Plugin.AllowsUi
            && HusbandrySkill.Reaches(HusbandrySkill.Local(), HusbandrySettings.LoreLevel.Value);

        public static string Duration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "";
            if (seconds < 60) return ShepherdLocalization.Format("time_seconds", Mathf.Max(1, Mathf.CeilToInt((float)seconds)));
            int minutes = Mathf.CeilToInt((float)(seconds / 60));
            return minutes < 60 ? ShepherdLocalization.Format("time_minutes", minutes)
                : ShepherdLocalization.Format("time_hours_minutes", minutes / 60, minutes % 60);
        }
        public static string Add(string text, string line) => string.IsNullOrEmpty(line) ? text
            : string.IsNullOrEmpty(text) ? line : text + "\n" + line;
        public static string Literal(string text)
        {
            text = (text ?? "").Replace('<', '‹').Replace('>', '›').Replace('\n', ' ').Replace('\r', ' ');
            return text.Length > 160 ? text.Substring(0, 160) : text;
        }
    }
}
