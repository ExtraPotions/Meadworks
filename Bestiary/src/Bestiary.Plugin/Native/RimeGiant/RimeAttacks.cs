// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The giant's three attacks, copies of the forest troll's own so its animations, timing and hit shapes stay the
    /// game's: the sweep (the troll's punch, with far more knockback), the slam (the troll's ground slam, used from
    /// further off because it sends the avalanche, <see cref="RimeAvalanche"/>), and the throw (the troll's rock throw,
    /// hurling an ice boulder, <see cref="RimeBoulder"/>). Every reach is grown with the body, which the game does not
    /// do on its own. The damage the settings own follows a change (<see cref="ApplyToAll"/>).
    /// </summary>
    public static class RimeAttacks
    {
        public const string Punch = "troll_punch";
        public const string GroundSlam = "troll_groundslam";
        public const string RockThrow = "troll_throw";
        public const string SweepName = "$item_ecp_rimegiant_sweep";
        public const string SlamName = "$item_ecp_rimegiant_slam";
        public const string ThrowName = "$item_ecp_rimegiant_throw";
        private const float SweepPush = 220f;     // the troll's punch: 100

        private static GameObject[] prefabs = new GameObject[0];

        /// <summary>One of the troll's attack items by name: they live in its weapon sets, not in the network scene.</summary>
        public static GameObject? TrollItem(Humanoid troll, string name) =>
            troll.m_randomSets.SelectMany(set => set.m_items).FirstOrDefault(item => item != null && item.name == name);

        public static GameObject[] Build(GameObject punch, GameObject slam, GameObject rockThrow, GameObject boulder)
        {
            GameObject sweep = Copy(punch, "ECP_RimeGiant_sweep", SweepName);
            GameObject avalanche = Copy(slam, "ECP_RimeGiant_slam", SlamName);
            Shared(avalanche).m_aiAttackRange = 12f;     // the wave carries it to you
            Shared(avalanche).m_aiAttackInterval = 12f;  // the troll's: 20
            GameObject hurl = Copy(rockThrow, "ECP_RimeGiant_throw", ThrowName);
            Shared(hurl).m_attack.m_attackProjectile = boulder;
            Shared(hurl).m_aiAttackRange = 22f;           // the troll's 20; its throw's arc is not made for further
            prefabs = new[] { sweep, avalanche, hurl };
            foreach (GameObject item in prefabs)
            {
                Apply(Shared(item));
            }
            return prefabs;
        }

        private static GameObject Copy(GameObject troll, string name, string token)
        {
            GameObject item = PrefabBench.Copy(troll, name);
            ItemDrop.ItemData.SharedData shared = Shared(item);
            shared.m_name = token;
            Grow(shared, RimeGiantCreature.Size);
            return item;
        }

        /// <summary>The attack's reach and the AI's ranges, grown with the body.</summary>
        private static void Grow(ItemDrop.ItemData.SharedData shared, float size)
        {
            Attack attack = shared.m_attack;
            attack.m_attackRange *= size;
            attack.m_attackHeight *= size;
            attack.m_attackRayWidth *= size;
            shared.m_aiAttackRange *= size;
            shared.m_aiAttackRangeMin *= size;
        }

        /// <summary>The numbers the settings own, onto one copy of an attack's shared data.</summary>
        public static void Apply(ItemDrop.ItemData.SharedData shared)
        {
            if (shared.m_name == SweepName)
            {
                shared.m_damages = new HitData.DamageTypes { m_blunt = RimeGiantSettings.SweepDamage };
                shared.m_attackForce = SweepPush;
            }
            else if (shared.m_name == SlamName)
            {
                shared.m_damages = new HitData.DamageTypes { m_blunt = RimeGiantSettings.SlamDamage };
            }
            else if (shared.m_name == ThrowName)
            {
                shared.m_damages = new HitData.DamageTypes { m_blunt = RimeGiantSettings.BoulderDamage / 2f, m_frost = RimeGiantSettings.BoulderDamage };
            }
        }

        /// <summary>After a settings change: the prefabs' attacks and every loaded giant's own copies of them.</summary>
        public static void ApplyToAll()
        {
            foreach (GameObject item in prefabs)
            {
                Apply(Shared(item));
            }
            foreach (Character character in Character.GetAllCharacters())
            {
                if (character is Humanoid humanoid && humanoid.GetComponent<RimeArmour>() != null)
                {
                    humanoid.GetInventory().GetAllItems().ForEach(item => Apply(item.m_shared));
                }
            }
        }

        private static ItemDrop.ItemData.SharedData Shared(GameObject item) => item.GetComponent<ItemDrop>().m_itemData.m_shared;
    }
}
