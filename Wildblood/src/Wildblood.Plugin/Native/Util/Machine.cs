// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Util
{
    /// <summary>
    /// What kind of machine this is. A dedicated server has no screen, no speakers and no local player, so nothing
    /// cosmetic it made would ever be seen or heard: effects make nothing there, while what they stand for - a cloud's
    /// poison, a blast's damage - still runs wherever it runs.
    /// </summary>
    internal static class Machine
    {
        /// <summary>True on a dedicated server.</summary>
        public static bool Headless => ZNet.instance != null && ZNet.instance.IsDedicated();
    }
}
