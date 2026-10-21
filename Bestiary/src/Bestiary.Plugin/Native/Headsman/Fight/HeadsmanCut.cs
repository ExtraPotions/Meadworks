// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// What hits in the slam, the low sweep and the spin: the axe head, not a sweep from the body. While a move's cut
    /// window plays (<see cref="HeadsmanMove.Cut"/>) a sphere of <see cref="HeadsmanSettings.AxeRadius"/> round the
    /// blade's edge, swept from where it was last frame to where it is now, hits what it touches once per swing: in the
    /// slam the spot where the axe lands, in the sweep the arc it drags across the front, in the spin the ring the head
    /// draws round the creature (standing inside it is safe). The hit
    /// is made the way the game's own melee hit is (the attack's damage and star scaling, push, stagger, blocking,
    /// parrying, dodging, hit effects), so it fights like any attack, but not through a wall (<see cref="Walled"/>). Decided on the
    /// creature's owner only.
    /// </summary>
    public sealed class HeadsmanCut
    {
        private readonly Humanoid boss;
        private readonly Collider[] touched = new Collider[32];
        private readonly HashSet<GameObject> struck = new HashSet<GameObject>();
        private static int Solids;
        private static readonly RaycastHit[] lineHits = new RaycastHit[16];
        private Vector3? last;

        public HeadsmanCut(Humanoid boss) => this.boss = boss;

        /// <summary>OWNER, each frame (`time` seconds into the move's clip); `edge` the blade's edge in the world.</summary>
        public void Step(HeadsmanMove? move, float time, Vector3 edge)
        {
            Attack? attack = boss.m_currentAttack;
            if (move == null || !move.Cutting(time) || attack?.m_weapon == null)
            {
                last = null;
                if (move == null || time < move.Cut.x)
                {
                    struck.Clear();
                }
                return;
            }
            Sweep(last ?? edge, edge, HeadsmanSettings.AxeRadius, attack);
            last = edge;
        }

        private void Sweep(Vector3 from, Vector3 to, float radius, Attack attack)
        {
            int count = Physics.OverlapCapsuleNonAlloc(from, to, radius, touched, Attack.m_attackMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                GameObject target = Projectile.FindHitObject(touched[i]);
                if (target == null || target == boss.gameObject || !struck.Add(target))
                {
                    continue;
                }
                if (target.GetComponent<IDestructible>() is IDestructible destructible && !Spared(target.GetComponent<Character>(), attack)
                    && !Walled(target.GetComponent<Character>()))
                {
                    Strike(destructible, touched[i], to, attack);
                }
            }
        }

        /// <summary>
        /// A creature behind a wall is not hit by a swing that grazes through it: a straight line from the Executioner's
        /// middle to the creature's middle must not cross a solid (terrain, a wall or other building piece, a rock).
        /// Pieces and other non-creatures are hit by touch, as before.
        /// </summary>
        private bool Walled(Character? foe)
        {
            if (foe == null)
            {
                return false;
            }
            if (Solids == 0)
            {
                Solids = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            }
            Vector3 from = boss.GetCenterPoint(), line = foe.GetCenterPoint() - from;
            float length = line.magnitude;
            if (length < 0.01f)
            {
                return false;
            }
            int count = Physics.RaycastNonAlloc(from, line / length, lineHits, length, Solids, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = lineHits[i].collider.transform;
                // The two bodies' own colliders (the axe, worn equipment, a ragdoll piece) are not walls.
                if (!hit.IsChildOf(boss.transform) && !hit.IsChildOf(foe.transform))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A friend is not hit; a foe dodging through the axe is not either (the swing is spent on them).</summary>
        private bool Spared(Character? foe, Attack attack)
        {
            if (foe == null)
            {
                return false;
            }
            if (!BaseAI.IsEnemy(boss, foe))
            {
                return true;
            }
            if (attack.m_weapon.m_shared.m_dodgeable && foe.IsDodgeInvincible())
            {
                (foe as Player)?.HitWhileDodging();
                return true;
            }
            return false;
        }

        private void Strike(IDestructible target, Collider collider, Vector3 edge, Attack attack)
        {
            ItemDrop.ItemData.SharedData shared = attack.m_weapon.m_shared;
            Vector3 point = collider.ClosestPoint(edge);
            float factor = boss.GetRandomSkillFactor(shared.m_skillType);
            HitData hit = Hit(attack, collider, point, factor);
            attack.ModifyDamage(hit, factor);
            boss.GetSEMan().ModifyAttack(shared.m_skillType, ref hit);
            shared.m_hitEffect.Create(point, Quaternion.identity, null, 1f, -1, boss.GetZDOID());
            attack.m_hitEffect.Create(point, Quaternion.identity, null, 1f, -1, boss.GetZDOID());
            target.Damage(hit);
        }

        // The fields the game's melee hit fills, from the attack the creature is in; pushed straight out from the creature.
        private HitData Hit(Attack attack, Collider collider, Vector3 point, float factor)
        {
            ItemDrop.ItemData weapon = attack.m_weapon;
            Vector3 outward = Vector3.ProjectOnPlane(point - boss.transform.position, Vector3.up);
            var hit = new HitData
            {
                m_toolTier = (short)weapon.m_shared.m_toolTier, m_skillLevel = boss.GetSkillLevel(weapon.m_shared.m_skillType),
                m_itemLevel = (short)weapon.m_quality, m_itemWorldLevel = (byte)weapon.m_worldLevel,
                m_pushForce = weapon.m_shared.m_attackForce * factor * attack.m_forceMultiplier,
                m_backstabBonus = weapon.m_shared.m_backstabBonus, m_staggerMultiplier = attack.m_staggerMultiplier,
                m_dodgeable = weapon.m_shared.m_dodgeable, m_blockable = weapon.m_shared.m_blockable,
                m_skill = weapon.m_shared.m_skillType, m_damage = weapon.GetDamage(), m_point = point,
                m_dir = outward.sqrMagnitude > 1e-4f ? outward.normalized : boss.transform.forward,
                m_hitCollider = collider, m_hitType = HitData.HitType.EnemyHit, m_variant = weapon.m_shared.m_hitVariant,
            };
            hit.SetAttacker(boss);
            return hit;
        }
    }
}
