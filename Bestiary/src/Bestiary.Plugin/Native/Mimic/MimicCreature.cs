// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// The mimic creature: a copy of the Black Forest crypt skeleton, which keeps what the design takes from it - 40
    /// health, its damage modifiers (weak to blunt and fire, resistant to pierce and frost, immune to poison), the
    /// Undead faction, its hit and death sounds and its AI ranges - with the skeleton's body swapped for the mimic's.
    /// It carries one weapon, the bite; it sleeps until woken by a hit or by <see cref="MimicDisguise"/> when a player
    /// opens it; its death leaves the mimic corpse; and it drops the chest's loot (see <see cref="MimicLoot"/>).
    /// </summary>
    public static class MimicCreature
    {
        public static GameObject Build(GameObject skeleton, GameObject visual, GameObject bite, GameObject corpse)
        {
            GameObject creature = PrefabBench.Copy(skeleton, MimicPrefabs.Creature);
            ReplaceVisual(creature, visual);
            Arm(creature.GetComponent<Humanoid>(), bite, corpse);
            Dormant(creature.GetComponent<MonsterAI>());
            ShapeColliders(creature);
            creature.GetComponent<CharacterDrop>().m_drops.Clear();
            RemoveSkeletonParts(creature);
            creature.AddComponent<MimicDisguise>();
            creature.AddComponent<MimicLeap>();
            return creature;
        }

        private static void ReplaceVisual(GameObject creature, GameObject visual)
        {
            Object.DestroyImmediate(creature.transform.Find("Visual").gameObject);
            visual.transform.SetParent(creature.transform, false);
            visual.name = "Visual";
            var events = visual.AddComponent<CharacterAnimEvent>();
            events.m_footIK = false;          // a chest has no feet to plant
            events.m_headRotation = false;    // nor a head to turn
            Transform eye = creature.transform.Find("EyePos");
            if (eye != null)
            {
                eye.localPosition = new Vector3(0f, 0.6f, 0.35f);
            }
            PointEquipmentAt(creature.GetComponent<VisEquipment>(), GameMaterials.Find(visual.transform, "body"));
        }

        /// <summary>The skeleton's equipment joints (hands, back) pointed at the mimic's body bone; the bite has no model.</summary>
        private static void PointEquipmentAt(VisEquipment? equipment, Transform? body)
        {
            if (equipment == null)
            {
                return;
            }
            foreach (FieldInfo field in typeof(VisEquipment).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType == typeof(Transform))
                {
                    field.SetValue(equipment, body);
                }
            }
            equipment.m_bodyModel = null;
        }

        private static void Arm(Humanoid humanoid, GameObject bite, GameObject corpse)
        {
            humanoid.m_name = "$enemy_ecp_cryptmimic";
            humanoid.m_defaultItems = new[] { bite };
            humanoid.m_randomWeapon = new GameObject[0];
            humanoid.m_randomArmor = new GameObject[0];
            humanoid.m_randomShield = new GameObject[0];
            humanoid.m_randomSets = new Humanoid.ItemSet[0];
            humanoid.m_randomItems = new Humanoid.RandomItem[0];
            MimicDisguise.ApplyPace(humanoid);
            humanoid.m_deathEffects.m_effectPrefabs = DeathEffects(humanoid.m_deathEffects.m_effectPrefabs, corpse);
        }

        /// <summary>
        /// The skeleton's death, minus its burst of bones: its death sound, then the mimic's corpse (which carries the
        /// loot hand-off), facing and sized as the creature was.
        /// </summary>
        private static EffectList.EffectData[] DeathEffects(EffectList.EffectData[] skeleton, GameObject corpse)
        {
            var effects = new List<EffectList.EffectData>();
            foreach (EffectList.EffectData effect in skeleton)
            {
                if (effect.m_prefab != null && !effect.m_prefab.name.StartsWith("vfx_"))
                {
                    effects.Add(effect);
                }
            }
            effects.Add(new EffectList.EffectData
            {
                m_prefab = corpse, m_inheritParentRotation = true, m_inheritParentScale = true,
            });
            return effects.ToArray();
        }

        /// <summary>Asleep until something wakes it: never by players walking near, only by a hit or by being opened.</summary>
        private static void Dormant(MonsterAI ai)
        {
            ai.m_sleeping = true;
            ai.m_wakeupRange = 0f;
            ai.m_noiseWakeup = false;
            ai.m_fallAsleepDistance = 0f;
            ai.m_wakeUpDelayMin = 0f;
            ai.m_wakeUpDelayMax = 0f;
        }

        /// <summary>The character capsule inside the chest, and a box over the whole chest so every part of it can be hit.</summary>
        private static void ShapeColliders(GameObject creature)
        {
            var capsule = creature.GetComponent<CapsuleCollider>();
            capsule.radius = 0.43f;
            capsule.height = 0.86f;
            capsule.center = new Vector3(0f, 0.43f, 0f);
            var shell = new GameObject("chest_shell") { layer = creature.layer };
            shell.transform.SetParent(creature.transform, false);
            var box = shell.AddComponent<BoxCollider>();
            box.size = new Vector3(2.0f, 0.62f, 0.86f);
            box.center = new Vector3(0f, 0.31f, 0f);
        }

        /// <summary>Parts that expect the skeleton's body: its footsteps, its star colouring.</summary>
        private static void RemoveSkeletonParts(GameObject creature)
        {
            foreach (var type in new[] { typeof(FootStep), typeof(LevelEffects) })
            {
                Component part = creature.GetComponentInChildren(type, true);
                if (part != null)
                {
                    Object.DestroyImmediate(part);
                }
            }
        }
    }
}
