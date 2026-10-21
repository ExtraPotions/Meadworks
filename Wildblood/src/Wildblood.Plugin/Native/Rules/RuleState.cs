// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The single active rule set the rest of the mod reads, and the one place it changes. Whether the active set is
    /// this machine's own file or a server's pushed copy is decided elsewhere; everything that rolls or scales a
    /// creature simply reads <see cref="Active"/> and, if it cares, listens for <see cref="Changed"/>.
    /// </summary>
    public static class RuleState
    {
        public static RuleSet Active { get; private set; } = RuleDefaults.BuildDefault();

        /// <summary>Raised after the active set is replaced, on any machine.</summary>
        public static event Action? Changed;

        public static void Adopt(RuleSet set)
        {
            if (set == null)
            {
                return;
            }
            Active = set;
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Log.Error($"a rule-change handler threw: {e}");
            }
        }
    }
}
