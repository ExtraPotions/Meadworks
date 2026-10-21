// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: multi-phase bosses pay boss rewards on their final phase only.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Runes
{
    /// <summary>
    /// Boss prefabs the game flags as bosses that are not the final kill. Kall Fimbulbringer (Valheim 1.0.20) dies as
    /// FrozenKing (sets defeated_frozenking) and FrozenKing_p2 before FrozenKing_p3 (defeated_frozenking_p3, final).
    /// These earlier phases drop like ordinary creatures: no boss scrolls, gems or boss rarity odds.
    /// </summary>
    public static class BossPhases
    {
        private static readonly HashSet<string> Earlier = new HashSet<string>(StringComparer.Ordinal) { "FrozenKing", "FrozenKing_p2" };

        public static IReadOnlyCollection<string> EarlierPhases => Earlier;

        /// <summary>True for an earlier phase of a multi-phase boss (exact prefab name).</summary>
        public static bool IsEarlierPhase(string? prefab) => prefab != null && Earlier.Contains(prefab);
    }
}
