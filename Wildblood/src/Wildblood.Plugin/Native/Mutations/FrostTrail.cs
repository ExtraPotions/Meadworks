// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Frostbound's trail: patches of pale, glossy ice where it walks, each lasting `trail life` seconds. A player on
    /// one keeps only `grip` percent of their footing, so they slide, turn and stop slowly, and moves `slow` percent
    /// slower (<see cref="IceFooting"/>, the "Slick ice" status). The shared ground-trail mechanism
    /// (<see cref="GroundTrailField"/>) does the rest; the aura is <see cref="FrostAura"/>'s.
    /// </summary>
    public sealed class FrostTrail : GroundTrailField
    {
        private protected override TrailKind Kind => TrailKind.Frost;
    }
}
