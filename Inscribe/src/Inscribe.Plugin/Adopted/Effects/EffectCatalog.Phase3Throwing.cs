// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using static ExtraPotions.Inscribe.Adopted.Effects.EffectParamKind;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectPolarity;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectRoute;
using static ExtraPotions.Inscribe.Adopted.Effects.EffectScope;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Phase 3, throwing (features/effects-phase3.md, "Throwing"): a one-handed weapon's secondary attack thrown like a
    /// spear (<see cref="ThrowSwing"/>, <see cref="ThrownWeapon"/>), and what a thrown weapon - a spear or such a
    /// weapon - does when it comes down (<see cref="ThrownLanding"/>: <see cref="Recall"/>, <see cref="Apportation"/>).
    /// </summary>
    internal static partial class EffectCatalog
    {
        static partial void RegisterPhase3Throwing()
        {
            Add("throwable", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, M, "per-swing clone: this one-handed weapon's secondary attack is the spear throw");
            Add("recall", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, M, "prefix Projectile.SpawnOnHit: the thrown weapon goes back to the thrower's inventory and hand");
            Add("apportation", Hook, ItemLocal, ValueTypes.Flag, None, Raise, null, M, "prefix Projectile.SpawnOnHit: a thrown hit on a creature moves the thrower beside it");
        }
    }
}
