// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Guard/Desperation.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Desperation: below Desperation Health percent of max health, the core Damage Reduction is multiplied by
    /// Desperation Multiplier (<see cref="Reductions"/>). It follows Damage Reduction, so it does nothing at level 0.
    /// An icon shows while it holds (<see cref="DefenseEffects"/>).
    /// </summary>
    public static class Desperation
    {
        public static bool Holds(Player player)
        {
            float threshold = DefenseGuardSettings.DesperationHealth.Value / 100f;
            if (player == null || threshold <= 0f || DefenseGuardSettings.DesperationMultiplier.Value <= 1f)
                return false;
            if (DefenseSkill.LocalShare(DefenseSettings.DamageReduction.Value) <= 0f)
                return false;
            float max = player.GetMaxHealth();
            return max > 0f && player.GetHealth() > 0f && player.GetHealth() / max < threshold;
        }
    }
}
