// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Mudbound's trail: patches of thick, dark, wet mud where it walks, each lasting `trail life` seconds. A player in
    /// one moves `slow` percent slower, and for a moment after stepping out (the "Deep mud" status). The ground itself
    /// is never changed. The shared ground-trail mechanism (<see cref="GroundTrailField"/>) does the rest.
    /// </summary>
    public sealed class MudTrail : GroundTrailField
    {
        private protected override TrailKind Kind => TrailKind.Mud;
    }
}
