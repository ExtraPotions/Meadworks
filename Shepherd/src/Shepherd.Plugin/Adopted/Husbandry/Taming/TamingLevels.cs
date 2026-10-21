// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Taming/TamingLevels.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// The Taming Levels setting as a lookup: creature prefab name to the Husbandry level a keeper needs before taming
    /// makes progress ("Lox:30, Asksvin:50"). Parsed once per distinct setting value, case-insensitive; entries without a
    /// number are skipped. A creature not listed needs nothing.
    /// </summary>
    public static class TamingLevels
    {
        private static string parsedFrom;
        private static Dictionary<string, float> levels = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The level a keeper needs to tame <paramref name="prefabName"/>; 0 when it needs none.</summary>
        public static float Required(string prefabName)
        {
            string value = HusbandryTamingSettings.TamingLevels.Value ?? "";
            if (value != parsedFrom)
            {
                levels = Parse(value);
                parsedFrom = value;
            }
            return prefabName != null && levels.TryGetValue(prefabName, out float level) ? level : 0f;
        }

        /// <summary>A Taming Levels value as name to level; also read by the skill book's Husbandry page.</summary>
        internal static Dictionary<string, float> Parse(string value)
        {
            Dictionary<string, float> parsed = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in value.Split(','))
            {
                string[] pair = part.Split(':');
                if (pair.Length != 2 || !float.TryParse(pair[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float level))
                    continue;
                string name = pair[0].Trim();
                if (name.Length > 0)
                    parsed[name] = level;
            }
            return parsed;
        }
    }
}
