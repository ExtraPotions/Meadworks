// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood authority, lifecycle and existing stat/loot seams.

namespace ExtraPotions.Wildblood.Adopted.Horde
{
    /// <summary>
    /// What the horde does to one creature: while it is on, a wild creature (<see cref="WildTag"/>) that is not tamed has
    /// the health and attack multipliers and drops 1 / the spawn multiplier of its loot. Every other creature, and every
    /// creature while it is off, gets 1. Read where each number is used - the owner's health set-up
    /// (<see cref="Scaling.StatApplier"/>), the hit as it lands (<see cref="Patches.DamageScalingPatch"/>), the drop
    /// list (<see cref="HordeLoot"/>) - from the tag in the creature's ZDO and the synced settings, so every machine
    /// agrees. A tame is the player's own and keeps its strength.
    /// </summary>
    internal static class HordeFactors
    {
        public static float Health(Character creature, ZDO? zdo) => Weakened(creature, zdo) ? HordeSettings.Health : 1f;

        public static float Attack(Character creature, ZDO? zdo) => Weakened(creature, zdo) ? HordeSettings.Attack : 1f;

        public static float Drops(Character creature, ZDO? zdo) =>
            Weakened(creature, zdo) ? 1f / HordeSettings.Spawns : 1f;

        internal static bool Weakened(Character creature, ZDO? zdo) =>
            HordeSettings.Enabled && creature != null && WildTag.Is(zdo) && !creature.IsTamed() && !creature.IsBoss()
            && !ExtraPotions.Wildblood.Runtime.ProtectedCreatures.IsProtected(creature.gameObject);
    }
}
