// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The Crypt Executioner: a copy of the game's Skeleton, so it keeps the Skeleton's rig, hit shapes, senses, faction,
    /// resistances (weak to blunt and fire) and death (the game's skeletons have no ragdoll: they burst into bones), grown to <see cref="Size"/> times the Skeleton, carrying the bone
    /// greataxe (<see cref="HeadsmanKit"/>) and playing the bundle's clips through its own animator. It fights with its
    /// six attacks (<see cref="HeadsmanAttacks"/>) and nothing of the Skeleton's; it drops coins, bone fragments and,
    /// by the settings' chance, its axehead. On every peer <see cref="HeadsmanRig"/> draws what its clips cannot do; on
    /// its owner <see cref="HeadsmanBrain"/> turns it in the rear strike and makes the axe head's hits.
    /// </summary>
    public static class HeadsmanCreature
    {
        public const float Size = 1.25f;

        public static GameObject Build(GameObject skeleton, GameObject[] attacks, GameObject? axehead, ZNetScene scene)
        {
            GameObject boss = PrefabBench.Copy(skeleton, HeadsmanPrefabs.Creature);
            boss.transform.localScale = Vector3.one * Size;
            var humanoid = boss.GetComponent<Humanoid>();
            Arm(humanoid, attacks);
            Loot(boss.GetComponent<CharacterDrop>(), axehead, scene);
            Transform visual = boss.transform.Find("Visual");
            HeadsmanKit.Wear(visual);
            HeadsmanKit.Animate(visual.GetComponentInChildren<Animator>(true));
            boss.AddComponent<HeadsmanRig>();
            boss.AddComponent<HeadsmanBrain>();
            return boss;
        }

        private static void Arm(Humanoid humanoid, GameObject[] attacks)
        {
            humanoid.m_name = "$enemy_ecp_headsman";
            humanoid.m_health = HeadsmanSettings.Health;
            humanoid.m_defaultItems = attacks;
            humanoid.m_randomWeapon = new GameObject[0];
            humanoid.m_randomShield = new GameObject[0];
            humanoid.m_randomArmor = new GameObject[0];
            humanoid.m_randomSets = new Humanoid.ItemSet[0];
            humanoid.m_randomItems = new Humanoid.RandomItem[0];
        }

        /// <summary>Coins and bones, and the axehead by the settings' chance (<see cref="Reloot"/> keeps that current).</summary>
        private static void Loot(CharacterDrop drops, GameObject? axehead, ZNetScene scene)
        {
            drops.m_drops = new List<CharacterDrop.Drop>();
            Add(drops, scene.GetPrefab("Coins"), 20, 40, 1f);
            Add(drops, scene.GetPrefab("BoneFragments"), 4, 8, 1f);
            Add(drops, axehead, 1, 1, HeadsmanSettings.AxeheadChance);
        }

        private static void Add(CharacterDrop drops, GameObject? item, int min, int max, float chance)
        {
            if (item != null)
            {
                drops.m_drops.Add(new CharacterDrop.Drop { m_prefab = item, m_amountMin = min, m_amountMax = max, m_chance = chance, m_levelMultiplier = false });
            }
        }

        /// <summary>After a settings change: the axehead's chance on the prefab (a creature takes its drops from it as it dies).</summary>
        public static void Reloot(GameObject boss, GameObject? axehead)
        {
            foreach (CharacterDrop.Drop drop in boss.GetComponent<CharacterDrop>().m_drops.Where(d => d.m_prefab == axehead))
            {
                drop.m_chance = HeadsmanSettings.AxeheadChance;
            }
        }
    }
}
