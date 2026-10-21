// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The greataxe's combo in the player's own animator (AssetWorkshop Greataxe/GreataxeCombo, previewed and approved):
    /// the Battleaxe's three combo states keep their triggers, blends and events, and while the greataxe is in hand an
    /// override puts the game's greatsword whirl in the second (the spin) and, in the third (the Battleaxe's overhead),
    /// a copy of its clip slowed after the hit so the player recovers for <see cref="Recovery"/> seconds. The first is
    /// the Battleaxe's own slash. One override per game controller, shared by every player; swapping keeps each
    /// layer's state and every parameter, so nothing jumps.
    /// </summary>
    public static class GreataxeAnimations
    {
        public const float Recovery = 0.75f;
        private const float BlendOut = 0.3f;   // the game's blend out of the third combo state
        private const string Second = "BattleAxe2", Third = "BattleAxe_Combo3", Spin = "Greatsword BaseAttack (2)";

        private static readonly Dictionary<RuntimeAnimatorController, AnimatorOverrideController?> made =
            new Dictionary<RuntimeAnimatorController, AnimatorOverrideController?>();

        /// <summary>The greataxe's override of `game`, or null when the game's clips are not all there (logged once).</summary>
        public static AnimatorOverrideController? For(RuntimeAnimatorController game)
        {
            if (!made.TryGetValue(game, out AnimatorOverrideController? ours))
            {
                ours = made[game] = Make(game);
            }
            return ours;
        }

        private static AnimatorOverrideController? Make(RuntimeAnimatorController game)
        {
            AnimationClip? Clip(string name) => game.animationClips.FirstOrDefault(c => c != null && c.name == name);
            AnimationClip? second = Clip(Second), third = Clip(Third), spin = Clip(Spin);
            if (second == null || third == null || spin == null)
            {
                Log.Warn($"Executioner's Greataxe: the player's animator lacks {Second}, {Third} or {Spin}; the greataxe swings as a Battleaxe.");
                return null;
            }
            var ours = new AnimatorOverrideController(game) { name = "ecp_greataxe_player" };
            ours[second] = spin;
            ours[third] = Recovered(third);
            return ours;
        }

        /// <summary>
        /// A copy of the overhead: its own events up to its hit, then one Speed event slowing the rest so the rest and the
        /// blend out take <see cref="Recovery"/>.
        /// </summary>
        private static AnimationClip Recovered(AnimationClip game)
        {
            AnimationClip copy = Object.Instantiate(game);
            copy.name = "ecp_greataxe_overhead";
            AnimationEvent[] events = game.events;
            float hit = events.Where(e => e.functionName == "OnAttackTrigger" || e.functionName == "Hit").Select(e => e.time)
                .DefaultIfEmpty(game.length * 0.7f).First();
            var slow = new AnimationEvent { functionName = "Speed", floatParameter = (game.length - hit) / (Recovery - BlendOut), time = Mathf.Min(hit + 0.01f, game.length) };
            copy.events = events.Where(e => e.time <= hit || e.functionName != "Speed").Append(slow).OrderBy(e => e.time).ToArray();
            return copy;
        }

        /// <summary>The animator onto `controller`, keeping each layer's state and every parameter (<see cref="AnimatorSwap"/>).</summary>
        public static void Swap(Animator animator, RuntimeAnimatorController controller) => AnimatorSwap.Swap(animator, controller);
    }
}
