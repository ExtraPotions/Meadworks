// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Hits the local player lands on a character. Character.Damage runs where the hit is built - the attacker's own
    /// client for melee, the projectile's owner (the shooter) for projectiles - and then sends the hit to the target's
    /// owner, so every change made here travels inside the HitData and the target's owner applies it with its own
    /// resists. The target is known here, so this is where the target-dependent offense lives: Slayers, Press the
    /// Advantage, Deathblow, Cruel Opening, Hamstring (carried as a status effect the owner applies) and the leeches
    /// (an attacker-side estimate from the outgoing hit, capped by the target's remaining health: armor and resists
    /// are only known on the owner). Steel Rhythm's window opens here on a combo's last hit.
    /// </summary>
    internal static class OutgoingHits
    {
        /// <summary>Deathblow's "first hit only": the last few targets it fired on (a small ring, no allocation).</summary>
        private static readonly ZDOID[] Opened = new ZDOID[16];
        private static int _nextOpened;

        public static void OnHit(Player player, Character target, HitData hit)
        {
            if (target.IsDead() || ReferenceEquals(target, player))
            {
                return;
            }
            AggregateValues v = AggregateHost.Current;
            float bonus = FamilyBonus(v, target) + StaggerBonus(v, target, hit) + OpenerBonus(v, target);
            if (bonus != 0f)
            {
                hit.m_damage.Modify(1f + bonus);
            }
            CreatureSlow.Carry(v, target, hit);
            if (IsFoe(player, target))
            {
                Leech(player, v, Mathf.Min(DamageSlots.Combat(in hit.m_damage), target.GetHealth()));
            }
            CheckComboFinisher(player);
        }

        // The game's own rule for an attack's health return (Attack: enemy, or an aggravatable neutral). A hit on a
        // player whose PvP is off, or on one's own tamed or summoned creature, is refused by the target's owner, so it
        // must not feed the attacker; chop and pickaxe damage never hurt a creature, so they are not counted either.
        private static bool IsFoe(Player player, Character target)
        {
            BaseAI? ai = target.GetBaseAI();
            return BaseAI.IsEnemy(player, target) || (ai != null && ai.IsAggravatable());
        }

        private static float FamilyBonus(AggregateValues v, Character target)
        {
            float add = 0f;
            switch (target.GetFaction())
            {
                case Character.Faction.Undead: add += v.Slayer[AggregateValues.Undead]; break;
                case Character.Faction.AnimalsVeg: add += v.Slayer[AggregateValues.Beasts]; break;
                case Character.Faction.SeaMonsters: add += v.Slayer[AggregateValues.Sea]; break;
            }
            if (target.IsBoss() || target.GetFaction() == Character.Faction.Boss)
            {
                add += v.Slayer[AggregateValues.Boss];
            }
            return add;
        }

        // Cruel Opening multiplies the hit by its own sneak-attack bonus and clears it, so the owner cannot apply it twice.
        private static float StaggerBonus(AggregateValues v, Character target, HitData hit)
        {
            if (!target.IsStaggering())
            {
                return 0f;
            }
            float chance = v[EffectKind.ExploitStagger];
            if (chance > 0f && hit.m_backstabBonus > 1f && Random.value < chance)
            {
                hit.m_damage.Modify(hit.m_backstabBonus);
                hit.m_backstabBonus = 1f;
            }
            return v[EffectKind.StaggeredTargetDamage];
        }

        private static float OpenerBonus(AggregateValues v, Character target)
        {
            float bonus = v[EffectKind.LowHealthOpener];
            if (bonus <= 0f || target.GetHealthPercentage() >= 0.2f)
            {
                return 0f;
            }
            ZDOID id = target.GetZDOID();
            if (System.Array.IndexOf(Opened, id) >= 0)
            {
                return 0f;
            }
            Opened[_nextOpened] = id;
            _nextOpened = (_nextOpened + 1) % Opened.Length;
            return bonus;
        }

        private static void Leech(Player player, AggregateValues v, float dealt)
        {
            if (dealt <= 0f)
            {
                return;
            }
            float[] leech = v.Leech;
            if (leech[AggregateValues.Health] > 0f)
            {
                player.Heal(dealt * leech[AggregateValues.Health], showText: false);
            }
            if (leech[AggregateValues.Stamina] > 0f)
            {
                player.AddStamina(dealt * leech[AggregateValues.Stamina]);
            }
            if (leech[AggregateValues.Eitr] > 0f)
            {
                player.AddEitr(dealt * leech[AggregateValues.Eitr]);
            }
        }

        // "The third hit of a combo in rhythm": the current attack is the last level of a chain of three or more (the
        // game resets the chain when the next swing comes late, so reaching the last level means it was in rhythm).
        private static void CheckComboFinisher(Player player)
        {
            Attack? attack = player.m_currentAttack;
            if (attack != null && attack.m_attackChainLevels >= 3 && attack.m_currentAttackCainLevel == attack.m_attackChainLevels - 1)
            {
                CombatWindows.OnComboFinisher();
            }
        }
    }
}
