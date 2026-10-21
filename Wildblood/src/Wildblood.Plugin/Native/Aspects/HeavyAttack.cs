// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Which of a boss's blows is heavy enough to shake the ground, read from the game's own attack data so the rule holds
    /// for any boss, modded ones included. Heavy is a blow that comes down with its whole weight: an area attack (a
    /// stomp, a slam, a nova, a spin) or a melee blow whose main harm is blunt (a smash, not a cut or a stab) - and it
    /// must harm a player; the damage the game uses to fell trees and break rock does not count. Projectiles, spit,
    /// breath, summons, calls and taunts never are. Among the vanilla bosses that is Eikthyr's stomp, the Elder's stomp,
    /// Bonemass's punch, Yagluth's nova, the Queen's burst of stabbing legs, the Fader's tail spin, the Hive's punch and
    /// nearly every swing of the Frozen King's chains; Moder and the flying TheHive have none.
    /// The impact point follows the game's own geometry for the blow, dropped to the boss's feet.
    /// </summary>
    internal static class HeavyAttack
    {
        public static bool IsHeavy(Attack attack)
        {
            ItemDrop.ItemData? weapon = attack.GetWeapon();
            if (weapon == null)
            {
                return false;
            }
            HitData.DamageTypes damage = weapon.m_shared.m_damages;
            return attack.m_attackType switch
            {
                Attack.AttackType.Area => damage.m_blunt > 0f || Largest(damage) > 0f, // any harm at all
                Attack.AttackType.Horizontal or Attack.AttackType.Vertical =>
                    damage.m_blunt > 0f && damage.m_blunt >= Largest(damage), // blunt leads
                _ => false,
            };
        }

        // The largest harm to a player other than blunt. Chop and pickaxe are left out: they fell trees and break rock.
        private static float Largest(HitData.DamageTypes d) =>
            Mathf.Max(d.m_damage, d.m_slash, d.m_pierce, d.m_fire, d.m_frost, d.m_lightning, d.m_poison, d.m_spirit);

        /// <summary>
        /// Where the blow meets the ground: an area attack's own centre (the game's: out from its origin by the attack's
        /// reach and offset), or where a melee sweep ends (the game sweeps a sphere of the ray's width out to the reach,
        /// so the sphere comes to rest one width short of it) - at the height of the boss's feet.
        /// </summary>
        public static Vector3 ImpactPoint(Attack attack, Character boss)
        {
            Transform body = boss.transform;
            float reach = attack.m_attackType == Attack.AttackType.Area
                ? attack.m_attackRange
                : Mathf.Max(0f, attack.m_attackRange - attack.m_attackRayWidth);
            Vector3 point = Origin(attack, boss).position + body.forward * reach + body.right * attack.m_attackOffset;
            point.y = body.position.y;
            return point;
        }

        // The game's attack origin: the named joint on the boss's model, or the boss itself.
        private static Transform Origin(Attack attack, Character boss)
        {
            if (string.IsNullOrEmpty(attack.m_attackOriginJoint))
            {
                return boss.transform;
            }
            Transform? joint = Utils.FindChild(boss.GetVisual().transform, attack.m_attackOriginJoint);
            return joint != null ? joint : boss.transform;
        }
    }
}
