// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// A crossbowman's shot: a copy of its skeleton's own bow, so the game's AI picks and times it as it does the
    /// archer's, and the Skeleton controller plays the same "attack_bow" states (which the creature's animator fills with
    /// the crossbow clips). The bow's own model is taken off (the crossbow is the kit's), and a blunt bone bolt leaves
    /// the crossbow's muzzle at the clip's shot, straight at the target, with the game's arbalest sound. It hits as hard
    /// as that skeleton's bow (<see cref="XbowKind.BowDamage"/>, times `Damage Factor`), as blunt. Used from `Range`
    /// metres down to point blank, like the archer's bow. The settings' numbers follow a reload (<see cref="ApplyToAll"/>).
    /// </summary>
    public static class XbowShot
    {
        public const string Muzzle = "ecp_xbow_muzzle";
        public const string Name = "$item_ecp_skeletoncrossbow";
        private const float Spread = 1.5f;      // degrees either way; the archer's bow: 2

        private static readonly Dictionary<XbowKind, GameObject> shots = new Dictionary<XbowKind, GameObject>();

        public static GameObject? Of(XbowKind kind) => shots.TryGetValue(kind, out GameObject shot) ? shot : null;

        public static GameObject Build(XbowKind kind, GameObject bow, GameObject bolt, GameObject? fire)
        {
            GameObject shot = PrefabBench.Copy(bow, kind.Shot);
            Unstring(shot);
            ItemDrop.ItemData.SharedData shared = shot.GetComponent<ItemDrop>().m_itemData.m_shared;
            kind.BowDamage = shared.m_damages.GetTotalDamage();
            shared.m_name = Name;
            shared.m_aiAttackRangeMin = 0f;
            Launch(shared.m_attack, bolt, fire);
            Apply(shared, kind);
            shots[kind] = shot;
            return shot;
        }

        /// <summary>The bow's model hangs from the left hand's attach point; the crossbow is the creature's kit instead.</summary>
        private static void Unstring(GameObject shot)
        {
            Transform? attach = shot.transform.Find("attach");
            foreach (Transform child in attach?.Cast<Transform>().ToArray() ?? new Transform[0])
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void Launch(Attack attack, GameObject bolt, GameObject? fire)
        {
            attack.m_attackAnimation = "attack_bow";
            attack.m_attackType = Attack.AttackType.Projectile;
            attack.m_attackProjectile = bolt;
            attack.m_attackOriginJoint = Muzzle;
            attack.m_attackHeight = 0f;
            attack.m_attackRange = 0.1f;               // just in front of the muzzle
            attack.m_attackOffset = 0f;
            attack.m_projectileAccuracy = Spread;
            attack.m_projectileAccuracyMin = Spread;
            attack.m_launchAngle = 0f;
            attack.m_attackStamina = 0f;
            attack.m_speedFactor = 0f;                 // stands to shoot and to span
            attack.m_speedFactorRotation = 0.5f;       // and keeps turning after its target
            if (fire != null)
            {
                attack.m_triggerEffect.m_effectPrefabs = new[] { new EffectList.EffectData { m_prefab = fire } };
            }
        }

        /// <summary>The numbers the settings own, onto one copy of a kind's shot.</summary>
        public static void Apply(ItemDrop.ItemData.SharedData shared, XbowKind kind)
        {
            shared.m_aiAttackInterval = XbowSettings.ShotInterval;
            shared.m_aiAttackRange = XbowSettings.Range;
            shared.m_damages = new HitData.DamageTypes { m_blunt = kind.BowDamage * XbowSettings.DamageFactor };   // blunt-headed bolts
            shared.m_attack.m_projectileVel = XbowSettings.BoltSpeed;
            shared.m_attack.m_projectileVelMin = XbowSettings.BoltSpeed;
        }

        /// <summary>After a settings change: every kind's shot, and the copy each loaded crossbowman carries.</summary>
        public static void ApplyToAll()
        {
            foreach (var (kind, shot) in shots.Select(p => (p.Key, p.Value)))
            {
                Apply(shot.GetComponent<ItemDrop>().m_itemData.m_shared, kind);
            }
            foreach (Character character in Character.GetAllCharacters())
            {
                XbowKind? kind = XbowKind.OfCreature(Utils.GetPrefabName(character.gameObject));
                if (kind != null && character is Humanoid humanoid)
                {
                    foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems().Where(i => i.m_shared.m_name == Name))
                    {
                        Apply(item.m_shared, kind);
                    }
                }
            }
        }
    }
}
