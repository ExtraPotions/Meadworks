// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// The one check that needs both families: every class id an inscription lists must be an item class (the economy
    /// YAML's, or one a mod registered so far). An unknown id is a warning and ignored (classes-and-tiers.md section 3):
    /// its pool exists but no item is ever of that class. Run on every compose, logged by <see cref="ActiveRules"/>.
    /// </summary>
    internal static class ClassChecks
    {
        public static List<string> UnknownClasses(RuleSet rules, Func<string, bool> known)
        {
            List<string> warnings = new List<string>();
            foreach (AffixDef def in rules.Affixes.Affixes)
            {
                Check(def, def.BestClasses, known, warnings);
                Check(def, def.AllowedClasses, known, warnings);
            }
            return warnings;
        }

        private static void Check(AffixDef def, IReadOnlyList<string> ids, Func<string, bool> known, List<string> warnings)
        {
            foreach (string id in ids)
            {
                if (!known(id))
                {
                    warnings.Add($"inscriptions[{def.Id}].classes: '{id}' is not an item class (economy classes, or a mod's); ignored");
                }
            }
        }
    }
}
