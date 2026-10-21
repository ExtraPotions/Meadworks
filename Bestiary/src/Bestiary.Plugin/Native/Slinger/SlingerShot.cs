// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Slinger
{
    /// <summary>
    /// The slinger's shot: a copy of the Greydwarf's thrown rock turned into a slingshot. The same "throw" animation
    /// trigger (the creature's animator plays the shot clip there), a stone launched from the slingshot's fork on the
    /// clip's release, a little faster than a greydwarf throws, on the shallow arc that lands on the target's middle
    /// (<see cref="SlingerAim"/>), with a bow's twang. Used from `Range` metres down to point blank, like the skeleton
    /// archer's bow. The interval, range, speed and damage come from the slinger settings and follow a reload
    /// (<see cref="ApplyToAll"/>).
    /// </summary>
    public static class SlingerShot
    {
        public const string Muzzle = "ecr_sling_muzzle";
        private const float Spread = 2.5f;       // degrees either way

        public static ItemDrop.ItemData.SharedData Shared { get; private set; } = null!;

        public static GameObject Build(GameObject thrownRock, GameObject stone, GameObject? twang)
        {
            GameObject shot = PrefabBench.Copy(thrownRock, SlingerPrefabs.Shot);
            Shared = shot.GetComponent<ItemDrop>().m_itemData.m_shared;
            Shared.m_name = "$item_ecp_slinger_shot";
            Shared.m_aiAttackRangeMin = 0f;
            Shared.m_aiAttackMaxAngle = 10f;
            Shared.m_aiPrioritized = true;
            Launch(Shared.m_attack, stone, twang);
            Apply(Shared);
            return shot;
        }

        private static void Launch(Attack attack, GameObject stone, GameObject? twang)
        {
            attack.m_attackAnimation = "throw";
            attack.m_attackType = Attack.AttackType.Projectile;
            attack.m_attackProjectile = stone;
            attack.m_attackOriginJoint = Muzzle;
            attack.m_attackHeight = 0f;
            attack.m_attackRange = 0.15f;              // just in front of the fork
            attack.m_attackOffset = 0f;
            attack.m_projectileAccuracy = Spread;
            attack.m_projectileAccuracyMin = Spread;
            attack.m_launchAngle = 0f;                 // set for each shot by SlingerAim
            attack.m_attackStamina = 0f;
            attack.m_speedFactor = 0f;                 // stands to shoot
            attack.m_speedFactorRotation = 0.5f;       // and keeps turning after its target while it draws
            if (twang != null)
            {
                attack.m_triggerEffect.m_effectPrefabs = new[] { new EffectList.EffectData { m_prefab = twang } };
            }
        }

        /// <summary>The numbers the settings own, onto one copy of the shot's shared data.</summary>
        public static void Apply(ItemDrop.ItemData.SharedData shared)
        {
            shared.m_aiAttackInterval = SlingerSettings.ShotInterval;
            shared.m_aiAttackRange = SlingerSettings.Range;
            shared.m_damages = new HitData.DamageTypes { m_blunt = SlingerSettings.StoneDamage };
            shared.m_attack.m_projectileVel = SlingerSettings.StoneSpeed;
            shared.m_attack.m_projectileVelMin = SlingerSettings.StoneSpeed;
        }

        /// <summary>After a settings change: the prefab's shot and every loaded slinger's own copy of it.</summary>
        public static void ApplyToAll()
        {
            if (Shared == null)
            {
                return;
            }
            Apply(Shared);
            foreach (Character character in Character.GetAllCharacters())
            {
                if (character is Humanoid humanoid && humanoid.GetComponent<SlingRig>() != null)
                {
                    foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
                    {
                        if (item.m_shared.m_name == Shared.m_name)
                        {
                            Apply(item.m_shared);
                        }
                    }
                }
            }
        }
    }
}
