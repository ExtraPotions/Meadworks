// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// The twenty-three mutations (the last four from EliteCreaturesReborn 3.22, behavior only), declared in the order the specification lists them. That order is authoritative:
    /// it is the order names are assembled in and the order stars take their colours. The numeric value doubles
    /// as the bit index used when a creature's mutation set is packed into a single int for the ZDO.
    /// </summary>
    public enum Mutation
    {
        Mad = 0,
        Bloated = 1,
        Cloaked = 2,
        Splintering = 3,
        Leeching = 4,
        Warding = 5,
        Plated = 6,
        Miasmic = 7,
        Devouring = 8,
        Thieving = 9,
        Gilded = 10,
        Blinking = 11,
        Relentless = 12,
        Juggernaut = 13,
        Screecher = 14,
        Frostbound = 15,
        Mudbound = 16,
        Corrodent = 17,
        Cloning = 18,
        Flamebound = 19,
        Piercing = 20,
        Howling = 21,
        Binding = 22,
    }
}
