// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Carries an altar's locked draw - its stars and aspects, a Bountiful one's extras with them - across the one call
    /// that instantiates its boss. The altar patch sets it just before the game's delayed spawn and clears it just after;
    /// the boss wakes inside that call (Awake runs within the instantiate), and the lifecycle patch hands it to the first
    /// boss that claims it, before its controller rolls. Single-threaded and scoped to one call, so no other spawn can
    /// ever pick it up.
    /// </summary>
    internal static class AltarSummon
    {
        private static BossDraw? _pending;

        public static void Begin(BossDraw? draw) => _pending = draw;

        public static void End() => _pending = null;

        public static void Claim(EliteController controller)
        {
            if (_pending != null)
            {
                controller.ForceDraw(_pending.Value);
                _pending = null;
            }
        }
    }
}
