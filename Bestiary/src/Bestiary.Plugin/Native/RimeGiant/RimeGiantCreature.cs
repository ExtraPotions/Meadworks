// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The giant: a copy of the game's forest troll, so it keeps the troll's rig, animations, senses, sounds and hit
    /// shapes, grown to <see cref="Size"/> times the troll, frosted (<see cref="RimeLook"/>) and wearing the rime kit
    /// (<see cref="RimeKit"/>). It fights with the sweep, the slam and the ice boulder (<see cref="RimeAttacks"/>) and
    /// never with the troll's log; it is a mountain creature, immune to frost and weak to fire; it sleeps until woken
    /// (<see cref="RimeSlumber"/>); its plates turn physical hits aside until fire breaks them (<see cref="RimeArmour"/>).
    /// </summary>
    public static class RimeGiantCreature
    {
        public const float Size = 1.4f;

        public static GameObject Build(GameObject troll, GameObject[] attacks, GameObject corpse, GameObject kit, ZNetScene scene)
        {
            GameObject giant = PrefabBench.Copy(troll, RimeGiantPrefabs.Creature);
            giant.transform.localScale = Vector3.one * Size;
            var humanoid = giant.GetComponent<Humanoid>();
            Arm(humanoid, attacks);
            Harden(humanoid, scene);
            Asleep(giant.GetComponent<MonsterAI>(), scene);
            Die(humanoid, corpse, scene);
            Loot(giant.GetComponent<CharacterDrop>(), scene);
            Dress(giant, kit);
            giant.AddComponent<RimeArmour>();
            giant.AddComponent<RimeArmourLook>();
            giant.AddComponent<RimeSlumber>();
            giant.AddComponent<RimeAvalanche>();
            return giant;
        }

        private static void Arm(Humanoid humanoid, GameObject[] attacks)
        {
            humanoid.m_name = "$enemy_ecp_rimegiant";
            humanoid.m_defaultItems = attacks;
            humanoid.m_randomWeapon = new GameObject[0];
            humanoid.m_randomSets = new Humanoid.ItemSet[0];
            humanoid.m_randomItems = new Humanoid.RandomItem[0];
        }

        /// <summary>A mountain creature: immune to frost, weak to fire, otherwise the troll's resistances.</summary>
        private static void Harden(Humanoid humanoid, ZNetScene scene)
        {
            humanoid.m_faction = Character.Faction.MountainMonsters;
            humanoid.m_health = RimeGiantSettings.Health;
            humanoid.m_damageModifiers.m_frost = HitData.DamageModifier.Immune;
            humanoid.m_damageModifiers.m_fire = HitData.DamageModifier.Weak;
            humanoid.m_hitEffects.m_effectPrefabs = Swap(humanoid.m_hitEffects.m_effectPrefabs, "vfx_foresttroll_hit", scene.GetPrefab("vfx_frosttroll_hit"));
        }

        /// <summary>
        /// Born asleep; only <see cref="RimeSlumber"/> or a hit wakes it, never someone walking past. Asleep its body is
        /// fixed in place (the game's own switch for sleeping creatures), so nobody can shove the outcrop about.
        /// </summary>
        private static void Asleep(MonsterAI ai, ZNetScene scene)
        {
            ai.GetComponent<Character>().m_disableWhileSleeping = true;
            ai.m_sleeping = true;
            ai.m_wakeupRange = 0f;
            ai.m_noiseWakeup = false;
            ai.m_fallAsleepDistance = 0f;
            ai.m_wakeUpDelayMin = 0f;
            ai.m_wakeUpDelayMax = 0f;
            ai.m_enableHuntPlayer = false;
            ai.m_wakeupEffects.m_effectPrefabs = Effects(scene, "vfx_ice_destroyed", "sfx_ice_destroyed", "sfx_troll_alerted");
        }

        /// <summary>The troll's death with its ragdoll swapped for the giant's, and its plates shattering as it falls.</summary>
        private static void Die(Humanoid humanoid, GameObject corpse, ZNetScene scene)
        {
            var effects = humanoid.m_deathEffects.m_effectPrefabs
                .Where(effect => effect.m_prefab != null && effect.m_prefab.GetComponent<Ragdoll>() == null)
                .ToList();
            effects.Add(new EffectList.EffectData { m_prefab = corpse });
            effects.AddRange(Effects(scene, "vfx_ice_destroyed", "sfx_ice_destroyed"));
            humanoid.m_deathEffects.m_effectPrefabs = effects.ToArray();
        }

        /// <summary>Ice and cold from the heights, and silver from the mountain's heart.</summary>
        private static void Loot(CharacterDrop drops, ZNetScene scene)
        {
            drops.m_drops = new List<CharacterDrop.Drop>();
            Add(drops, scene.GetPrefab("Crystal"), 4, 8);
            Add(drops, scene.GetPrefab("FreezeGland"), 3, 6);
            Add(drops, scene.GetPrefab("SilverOre"), 3, 6);
        }

        private static void Add(CharacterDrop drops, GameObject? item, int min, int max)
        {
            if (item != null)
            {
                drops.m_drops.Add(new CharacterDrop.Drop { m_prefab = item, m_amountMin = min, m_amountMax = max, m_chance = 1f, m_levelMultiplier = true });
            }
        }

        /// <summary>The kit wears the troll's own material (unfrosted, so the ice keeps its baked colour); the skin frosts.</summary>
        private static void Dress(GameObject giant, GameObject kit)
        {
            Transform visual = giant.transform.Find("Visual");
            RimeKit.Wear(visual, kit, RimeKit.Skin(visual));
            RimeLook.Tint(visual);
            RimeLook.StarLooks(giant);
        }

        private static EffectList.EffectData[] Swap(EffectList.EffectData[] effects, string name, GameObject? with)
        {
            foreach (EffectList.EffectData effect in effects)
            {
                if (with != null && effect.m_prefab != null && effect.m_prefab.name == name)
                {
                    effect.m_prefab = with;
                }
            }
            return effects;
        }

        /// <summary>The named effects the game has, as an effect list.</summary>
        public static EffectList.EffectData[] Effects(ZNetScene scene, params string[] names) =>
            names.Select(scene.GetPrefab).Where(prefab => prefab != null)
                .Select(prefab => new EffectList.EffectData { m_prefab = prefab }).ToArray();
    }
}
