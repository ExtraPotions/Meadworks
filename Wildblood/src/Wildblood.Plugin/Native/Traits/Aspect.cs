// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// The boss aspects: the one modifier a boss carries in place of mutations. The numeric value is what a boss's and
    /// an altar's ZDO store, so it is fixed - append new aspects, never renumber. <see cref="None"/> is the plain
    /// vanilla fight and a real outcome of every roll, not the absence of one.
    /// </summary>
    public enum Aspect
    {
        None = 0,
        Reflective = 1,
        Shielded = 2,
        Mending = 3,
        Summoner = 4,
        Elementalist = 5,
        Enraged = 6,
        Twin = 7,
        Phantom = 8,
        Adaptive = 9,
        Fixated = 10,
        Stormbound = 11,
        Gravitic = 12,
        Colossal = 13,
        Tethered = 14,
        Bountiful = 15,
        Portalbound = 16,
        Nightfall = 17,
        Brutal = 18,
        Echoing = 19,
    }
}
