// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: EliteCreaturesReborn 3.22's Flamebound mutation, built from its described behavior only.
namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Flamebound's trail: patches of burning ground where it walks, each lasting `trail life` seconds. A player standing
    /// in one is set on fire (<see cref="FireGround"/>). It is the shared ground-trail mechanism
    /// (<see cref="GroundTrailField"/>) with a third kind, so it is decided on the owner and laid on every machine exactly as
    /// Frostbound's ice and Mudbound's mud are.
    /// </summary>
    public sealed class FlameTrail : GroundTrailField
    {
        private protected override TrailKind Kind => TrailKind.Fire;
    }
}
