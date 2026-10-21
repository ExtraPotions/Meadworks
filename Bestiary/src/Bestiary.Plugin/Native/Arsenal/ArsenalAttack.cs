// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// An arsenal skeleton's weapon, an item only it carries. A melee weapon is a copy of the sword of the skeleton it
    /// replaces, so the game's AI picks and times it as it does the sword, and it hits with that sword's damage turned
    /// into the weapon's blow (<see cref="Swing"/>): its kind, times the weapon's factor; the sword's chop stays. The bow is a copy of that skeleton's bow, its damage too, and
    /// shoots its arrow as the archer does, the arrow wearing the bone arrow. The game's model is taken off the "attach"
    /// and the bone one put in.
    /// </summary>
    public static class ArsenalAttack
    {
        private static readonly List<GameObject> attacks = new List<GameObject>();

        /// <summary>Every skeleton's weapon built.</summary>
        public static IEnumerable<GameObject> Items => attacks;

        public static GameObject? Build(ArsenalKind kind, ArsenalWeapon weapon, Humanoid skeleton, GameObject model, Material skin, GameObject? arrow)
        {
            GameObject? game = Game(kind, weapon, skeleton);
            if (game == null)
            {
                return null;
            }
            GameObject attack = PrefabBench.Copy(game, kind.Attack(weapon));
            Transform slot = attack.transform.Find("attach");
            ArsenalLook.Clear(slot);
            ArsenalLook.Wear(slot, model, skin);
            ItemDrop.ItemData.SharedData shared = attack.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_name = "$item_" + weapon.Word;
            if (weapon.Swing != null)
            {
                Swing(shared, weapon.Swing, weapon.TwoHanded);
            }
            else
            {
                Shoot(slot, shared, arrow, skin);
            }
            attacks.Add(attack);
            return attack;
        }

        /// <summary>The skeleton's own sword (or bow, for the bow), one of its random weapons, or null when the game lacks it.</summary>
        private static GameObject? Game(ArsenalKind kind, ArsenalWeapon weapon, Humanoid skeleton)
        {
            string name = weapon.IsBow ? kind.Bow : kind.Sword;
            GameObject? game = skeleton.m_randomWeapon.FirstOrDefault(item => item != null && item.name == name);
            if (game == null || game.transform.Find("attach") == null)
            {
                Log.Warn($"Arsenal skeleton {kind.Creature(weapon)} not built: {kind.Skeleton} has no {name} with an attach.");
                return null;
            }
            return game;
        }

        private static void Swing(ItemDrop.ItemData.SharedData shared, Swing swing, bool twoHanded)
        {
            shared.m_damages = Blown(shared.m_damages, swing);
            shared.m_itemType = twoHanded ? ItemDrop.ItemData.ItemType.TwoHandedWeapon : ItemDrop.ItemData.ItemType.OneHandedWeapon;
            shared.m_attack.m_attackAnimation = swing.Animation;
            shared.m_attack.m_attackRange = swing.Reach;
            shared.m_attack.m_attackAngle = swing.Angle;
            shared.m_aiAttackRange = swing.Reach - 0.2f;
            shared.m_aiAttackInterval = swing.Interval;
        }

        /// <summary>The bone bow strung as the archer holds it, loosing the bone arrow (the archer's own when that was not built).</summary>
        private static void Shoot(Transform slot, ItemDrop.ItemData.SharedData shared, GameObject? arrow, Material skin)
        {
            ArsenalLook.String(slot, ArsenalLook.SkeletonBow, skin);
            if (arrow != null)
            {
                shared.m_attack.m_attackProjectile = arrow;
            }
        }

        private static HitData.DamageTypes Blown(HitData.DamageTypes sword, Swing swing)
        {
            float total = (sword.m_slash + sword.m_pierce + sword.m_blunt) * swing.Factor;
            var damage = new HitData.DamageTypes { m_chop = sword.m_chop, m_pickaxe = sword.m_pickaxe };
            damage.m_slash = swing.Blow == Blow.Slash ? total : swing.Blow == Blow.SlashPierce ? total / 2f : 0f;
            damage.m_pierce = swing.Blow == Blow.Pierce ? total : swing.Blow == Blow.SlashPierce ? total / 2f : 0f;
            damage.m_blunt = swing.Blow == Blow.Blunt ? total : 0f;
            return damage;
        }
    }
}
