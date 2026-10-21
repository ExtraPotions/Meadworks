// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Bestiary.Runtime.Native.Slinger
{
    /// <summary>
    /// The slinger creature: a copy of the game's Greydwarf, so it keeps the Greydwarf's 40 health, resistances,
    /// faction, sounds, star looks, AI senses and loot, with these changes: it fights like the game's skeleton archer
    /// (the slingshot is its only weapon, and it does not circle its target), it wears the kit (slingshot in the left
    /// fist, satchel on the hip) and plays the bundle's shot in its animator's "throw" state, and it drops a few more
    /// stones. Its pouch and bands follow the shot (<see cref="SlingRig"/>).
    /// </summary>
    public static class SlingerCreature
    {
        private const string Kit = "ecr_slinger_kit";
        private const string Clip = "ecr_sling_shot";
        private const string ThrowClip = "Throw";   // the Greydwarf controller's clip in its "throw" state
        public const string ThrownRock = "Greydwarf_throw";   // one of the Greydwarf's default items; not a network prefab

        public static GameObject Build(GameObject greydwarf, GameObject shot, GameObject stone, AssetBundle bundle, GameObject? stoneItem)
        {
            GameObject creature = PrefabBench.Copy(greydwarf, SlingerPrefabs.Creature);
            Arm(creature.GetComponent<Humanoid>(), shot);
            StandAndShoot(creature.GetComponent<MonsterAI>());
            MoreStones(creature.GetComponent<CharacterDrop>(), stoneItem);
            Transform visual = creature.transform.Find("Visual");
            SlingerKit.Wear(visual, EmbeddedBundle.Prefab(bundle, Kit), Skin(visual), stone);
            PlayShot(visual.GetComponent<Animator>(), bundle.LoadAsset<AnimationClip>(Clip));
            creature.AddComponent<SlingRig>();
            return creature;
        }

        /// <summary>
        /// The slingshot is its only weapon, as the bow is the skeleton archer's: the claw and the thrown rock go, so
        /// the game's AI walks up to shooting range, stops and shoots, however close you are.
        /// </summary>
        private static void Arm(Humanoid humanoid, GameObject shot)
        {
            humanoid.m_name = "$enemy_ecp_greydwarfslinger";
            humanoid.m_defaultItems = new[] { shot };
        }

        /// <summary>
        /// The Greydwarf's AI runs around its target for 3 seconds in every 6; the skeleton archer's never does, so
        /// between shots the slinger stands and turns after its target.
        /// </summary>
        private static void StandAndShoot(MonsterAI ai)
        {
            ai.m_circleTargetInterval = 0f;
        }

        /// <summary>Two to four stones on top of the Greydwarf's own drops: the satchel's.</summary>
        private static void MoreStones(CharacterDrop? drops, GameObject? stoneItem)
        {
            if (drops == null || stoneItem == null)
            {
                return;
            }
            drops.m_drops.Add(new CharacterDrop.Drop
            {
                m_prefab = stoneItem, m_amountMin = 2, m_amountMax = 4, m_chance = 1f, m_levelMultiplier = true,
            });
        }

        /// <summary>The Greydwarf's own body material, which the kit's parts copy so they are lit like the creature.</summary>
        private static Material Skin(Transform visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Renderer body = renderers.FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("greydwarf", StringComparison.OrdinalIgnoreCase))
                ?? renderers.OrderByDescending(r => r.bounds.size.sqrMagnitude).First();
            return body.sharedMaterial;
        }

        /// <summary>The shot plays wherever the Greydwarf's controller would play its throw, on every peer.</summary>
        private static void PlayShot(Animator animator, AnimationClip? clip)
        {
            // The stone leaves from the sling's muzzle bone; a culled animator leaves that bone where it was, so a slinger
            // nobody sees (out of view, or owned by a server) would throw from the wrong place.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            AnimationClip? original = controller.animationClips.FirstOrDefault(c => c.name == ThrowClip);
            if (clip == null || original == null)
            {
                Log.Warn($"Greydwarf slinger: no {(clip == null ? Clip + " in the bundle" : ThrowClip + " clip in the Greydwarf's animator")}; it throws like a greydwarf.");
                return;
            }
            var shooting = new AnimatorOverrideController(controller) { name = "ecp_slinger_animator" };
            shooting[original] = clip;
            animator.runtimeAnimatorController = shooting;
        }
    }
}
