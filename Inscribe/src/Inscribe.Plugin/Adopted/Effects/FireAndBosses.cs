// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    // Damage taken by the local player from bosses and fire, on that player's own client (the game resolves a player's
    // incoming hits, its status effects and its burning ticks on the machine that owns the player). Nothing is sent.

    /// <summary>
    /// The local player's share of the damage dispatchers (<see cref="IncomingDamageDispatch"/>,
    /// <see cref="HealthLossDispatch"/>), which have already tested that the target is the local player.
    /// </summary>
    internal static class FireAndBossHits
    {
        /// <summary>
        /// Forsaken Ward (<c>boss_damage_taken</c>): a hit whose attacker is a boss (Character.m_boss: its weapons, its
        /// projectiles and its area attacks all carry it) deals X% less, scaled before the game's own pipeline like the
        /// other percent resists (<see cref="IncomingHits"/>), so it multiplies with blocking, resistances and armor.
        /// </summary>
        public static void ReduceBossHit(HitData hit)
        {
            float less = AggregateHost.Current[EffectKind.BossDamageTaken];
            if (less <= 0f || !hit.HaveAttacker())
            {
                return;
            }
            Character attacker = hit.GetAttacker();
            if (attacker != null && attacker.IsBoss())
            {
                hit.m_damage.Modify(Mathf.Max(0f, 1f - less));
            }
        }

        /// <summary>
        /// Ember Skin (<c>burning_taken</c>): each Burning tick (SE_Burning calls ApplyDamage directly) and each lava hit
        /// (HitType.AshlandsLava, through RPC_Damage) is X% lower. Ahead of the Runic Ward in the same dispatcher, so the
        /// ward absorbs the reduced amount.
        /// </summary>
        public static void ReduceBurning(HitData hit)
        {
            float less = AggregateHost.Current[EffectKind.BurningTaken];
            if (less <= 0f)
            {
                return;
            }
            if (hit.m_hitType == HitData.HitType.Burning || hit.m_hitType == HitData.HitType.AshlandsLava)
            {
                hit.m_damage.Modify(Mathf.Max(0f, 1f - less));
            }
        }
    }

    /// <summary>
    /// Quench (<c>burning_decay</c>): Burning on the local player ends X% sooner. Its clock runs 1/(1 - X) as fast (the
    /// Purity pattern, <see cref="DebuffDecayPatch"/>, which adds its own share and calls this from the same postfix),
    /// so it lasts (1 - X) of its time and the fire it still held is never dealt. X is held below 90% whatever the cap
    /// says.
    /// </summary>
    internal static class BurningDecay
    {
        private const float MostSooner = 0.9f;

        /// <summary>The local player's status effect, <paramref name="sooner"/> above 0.</summary>
        public static void Apply(StatusEffect effect, float dt, float sooner)
        {
            if (effect is SE_Burning)
            {
                sooner = Mathf.Min(sooner, MostSooner);
                effect.m_time += dt * sooner / (1f - sooner);
            }
        }
    }
}
