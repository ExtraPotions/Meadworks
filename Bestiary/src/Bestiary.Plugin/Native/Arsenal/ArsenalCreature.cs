// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// An arsenal skeleton: a copy of one of the game's archer skeletons (<see cref="ArsenalKind"/>), so it keeps that
    /// skeleton's health, resistances, faction (the undead), sounds, senses, AI, star looks and drops, with these changes:
    /// its only weapon is one of the arsenal's (<see cref="ArsenalAttack"/>), it keeps the Skeleton's random shield
    /// beside a one-handed weapon (none with the atgeir or the bow), it strikes one blow at a time
    /// (<see cref="ArsenalClips"/>), and it may drop a spine on top of the skeleton's own drops.
    /// </summary>
    public static class ArsenalCreature
    {
        private const float SpineChance = 0.1f;

        public static GameObject Build(ArsenalKind kind, ArsenalWeapon weapon, GameObject skeleton, GameObject attack, GameObject? spine)
        {
            GameObject creature = PrefabBench.Copy(skeleton, kind.Creature(weapon));
            Arm(creature.GetComponent<Humanoid>(), weapon, attack);
            AddSpine(creature.GetComponent<CharacterDrop>(), spine);
            Animator? animator = creature.transform.Find("Visual")?.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                ArsenalClips.Animate(animator, weapon, creature.name);
            }
            return creature;
        }

        private static void Arm(Humanoid humanoid, ArsenalWeapon weapon, GameObject attack)
        {
            humanoid.m_name = "$enemy_" + weapon.EnemyWord;
            humanoid.m_defaultItems = new[] { attack };
            humanoid.m_randomWeapon = new GameObject[0];
            humanoid.m_randomSets = new Humanoid.ItemSet[0];
            if (weapon.TwoHanded || weapon.IsBow)
            {
                humanoid.m_randomShield = new GameObject[0];
            }
        }

        /// <summary>One spine one time in ten, whatever its stars (every arsenal skeleton, the crossbowman too).</summary>
        public static void AddSpine(CharacterDrop? drops, GameObject? spine)
        {
            if (drops == null || spine == null)
            {
                return;
            }
            drops.m_drops.Add(new CharacterDrop.Drop
            {
                m_prefab = spine, m_amountMin = 1, m_amountMax = 1, m_chance = SpineChance, m_levelMultiplier = false,
            });
        }
    }
}
