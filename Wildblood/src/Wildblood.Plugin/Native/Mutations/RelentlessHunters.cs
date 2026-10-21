// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Every loaded Relentless creature that has a monster AI, keyed by that AI. The Relentless patches sit on game AI
    /// methods that run for every monster many times a second, so each answers "not one of ours" with a single
    /// dictionary lookup, and with none at all while no Relentless creature is loaded.
    /// </summary>
    public static class RelentlessHunters
    {
        private static readonly Dictionary<BaseAI, RelentlessBehaviour> Hunters = new Dictionary<BaseAI, RelentlessBehaviour>();

        public static void Add(BaseAI ai, RelentlessBehaviour hunter) => Hunters[ai] = hunter;

        public static void Remove(BaseAI ai) => Hunters.Remove(ai);

        public static bool TryGet(BaseAI ai, out RelentlessBehaviour hunter)
        {
            if (Hunters.Count == 0)
            {
                hunter = null!;
                return false;
            }
            return Hunters.TryGetValue(ai, out hunter) && hunter.Active;
        }

        public static bool Has(BaseAI ai) => TryGet(ai, out _);

        /// <summary>True on the owner while this creature holds a quarry within its chase distance.</summary>
        public static bool IsHunting(BaseAI ai) => TryGet(ai, out RelentlessBehaviour hunter) && hunter.Hunting;
    }
}
