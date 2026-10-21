// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Tally
{
    /// <summary>The boss tallies' write timer, on the plugin's own object: one test a frame while no boss is being hit.</summary>
    internal sealed class TallyFlush : MonoBehaviour
    {
        private void Update() => Guard.Run("DamageTally.Flush", static now => DamageTally.FlushDue(now), Time.time);
    }
}
