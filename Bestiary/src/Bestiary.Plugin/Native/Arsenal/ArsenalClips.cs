// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// The single blows of the arsenal skeletons. The Skeleton's "attack" state plays one sword swing (the Draugr's
    /// "Standing Melee Attack Horizontal") and hands back to idle; a skeleton with a dagger, axe, spear or atgeir plays
    /// the first blow of the player's own attack for that weapon in its place (humanoid clips, which play on the
    /// Skeleton as they do on the player), through an override of its animator, so its AI, timing and network sync stay
    /// the game's. The player's clip carries its own hit event; a copy of the clip is used whose hit comes no later than
    /// <see cref="LatestHit"/> of the way through, before the Skeleton's state starts handing back to idle.
    /// </summary>
    public static class ArsenalClips
    {
        public const string GameSwing = "Standing Melee Attack Horizontal";
        private const float LatestHit = 0.75f;
        private static readonly string[] Hits = { "OnAttackTrigger", "Hit" };

        private static readonly Dictionary<string, AnimationClip> singles = new Dictionary<string, AnimationClip>();
        private static AnimationClip[] playerClips = new AnimationClip[0];

        /// <summary>The player's clips, from the Player prefab's animator.</summary>
        public static void Load(ZNetScene scene)
        {
            Animator? animator = scene.GetPrefab("Player")?.GetComponentInChildren<Animator>(true);
            playerClips = animator?.runtimeAnimatorController?.animationClips ?? new AnimationClip[0];
            if (playerClips.Length == 0)
            {
                Log.Warn("Arsenal skeletons: the Player's animator was not found; they all swing like the Skeleton.");
            }
        }

        /// <summary>The weapon's blow in place of the Skeleton's sword swing, on this creature's animator.</summary>
        public static void Animate(Animator animator, ArsenalWeapon weapon, string creature)
        {
            string? name = weapon.Swing?.Clip;
            if (name == null)
            {
                return;
            }
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            AnimationClip? swing = controller.animationClips.FirstOrDefault(c => c.name == GameSwing);
            AnimationClip? blow = Single(name);
            if (swing == null || blow == null)
            {
                Log.Warn($"Arsenal skeleton {creature}: no {(blow == null ? name + " clip on the Player" : GameSwing + " clip on the Skeleton")}; it swings like the Skeleton.");
                return;
            }
            var single = new AnimatorOverrideController(controller) { name = creature + "_animator" };
            single[swing] = blow;
            animator.runtimeAnimatorController = single;
        }

        private static AnimationClip? Single(string name)
        {
            if (singles.TryGetValue(name, out AnimationClip made))
            {
                return made;
            }
            AnimationClip? game = playerClips.FirstOrDefault(c => c != null && c.name == name);
            if (game == null)
            {
                return null;
            }
            AnimationClip clip = Object.Instantiate(game);
            clip.name = "ecp_arsenal_" + name;
            clip.events = game.events.Select(e => Early(e, game.length)).ToArray();
            singles[name] = clip;
            return clip;
        }

        private static AnimationEvent Early(AnimationEvent e, float length)
        {
            if (Hits.Contains(e.functionName))
            {
                e.time = Mathf.Min(e.time, length * LatestHit);
            }
            return e;
        }
    }
}
