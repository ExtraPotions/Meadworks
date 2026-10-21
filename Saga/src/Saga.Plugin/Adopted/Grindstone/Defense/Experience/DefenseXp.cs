// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Experience/DefenseXp.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Defense experience, on the local player's own client, once per hit that trains it (<see cref="IncomingHit"/>:
    /// from a creature, or from a player while PvP hits train), when the hit is over:
    /// <list type="bullet">
    /// <item>blocked: Block Experience, times Parry Multiplier for a parry, times Weapon Block Share without a shield,
    /// times First Block Bonus the first time the character blocks that kind of creature (<see cref="FoeDiscovery"/>);</item>
    /// <item>not blocked but it hurt: Hit Taken Experience;</item>
    /// </list>
    /// each times the hit's size, √(damage as it arrived ÷ Hit Size Damage) between Min and Max Hit Size, so late-game
    /// hits train faster and good armour does not slow training, and times Experience Multiplier. After a hit earned
    /// experience, the next can only after Experience Cooldown. The world's skill-gain modifier applies as to every skill.
    /// </summary>
    public static class DefenseXp
    {
        private static float lastCredit = float.NegativeInfinity;

        public static void OnHit(Player player)
        {
            if (Time.time - lastCredit < DefenseExperienceSettings.Cooldown.Value)
                return;
            float amount = IncomingHit.Blocked ? BlockAmount(player) : HitAmount();
            amount *= Size(IncomingHit.RawDamage) * Mathf.Max(0f, DefenseExperienceSettings.Multiplier.Value);
            if (amount <= 0f)
                return;
            lastCredit = Time.time;
            player.RaiseSkill(DefenseSkill.Type, amount);
        }

        private static float BlockAmount(Player player)
        {
            float amount = Mathf.Max(0f, DefenseExperienceSettings.BlockExperience.Value);
            if (IncomingHit.Parried)
                amount *= Mathf.Max(0f, DefenseExperienceSettings.ParryMultiplier.Value);
            if (!IncomingHit.WithShield)
                amount *= Mathf.Clamp01(DefenseExperienceSettings.WeaponBlockShare.Value / 100f);
            if (amount > 0f && FoeDiscovery.TryRecord(player, IncomingHit.Attacker))
                amount *= Mathf.Max(1f, DefenseExperienceSettings.FirstBlockBonus.Value);
            return amount;
        }

        private static float HitAmount() =>
            IncomingHit.Taken > 0f ? Mathf.Max(0f, DefenseExperienceSettings.HitExperience.Value) : 0f;

        /// <summary>The size of a hit of this raw damage.</summary>
        public static float Size(float damage)
        {
            float reference = Mathf.Max(1f, DefenseExperienceSettings.HitSizeDamage.Value);
            float min = Mathf.Max(0f, DefenseExperienceSettings.MinHitSize.Value);
            float max = Mathf.Max(min, DefenseExperienceSettings.MaxHitSize.Value);
            return Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, damage) / reference), min, max);
        }
    }
}
