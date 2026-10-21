// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Item levels other mods set through the API (<c>SetItemLevel</c>, api.md section 2): prefab name to level 1-8. The
    /// derivation reads them right under the economy YAML's <c>item_tiers.items</c> (a YAML entry for the same prefab
    /// wins) and above the material, station and fallback steps (<see cref="TierDerivation"/>). Code, not data: every
    /// peer runs the same mods and sets the same levels. Main thread only.
    /// </summary>
    internal static class CodeLevels
    {
        private static readonly Dictionary<string, int> Levels = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Sets a prefab's level (replacing an earlier one) and drops the cached levels.</summary>
        public static void Set(string prefab, int level)
        {
            Levels[prefab] = Math.Max(1, Math.Min(TierResult.MaxLevel, level));
            ItemTier.Forget();
        }

        public static bool TryGet(string prefab, out int level) => Levels.TryGetValue(prefab, out level);
    }
}
