// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// How much grip this machine's own player has underfoot: 1 everywhere but on a Frostbound patch, where it falls to
    /// the patch's `grip`. On the ground the game moves a player by pushing their body, every physics step, all the way
    /// from how it is moving to how the player means it to move; with less grip only that share of the push lands (the
    /// rest is lost to the ice), and the friction of their feet on the ground falls by the same share. So a player on
    /// ice keeps sliding the way they were going, is slow to set off, slow to turn and slow to stop, and drifts
    /// downhill on a slope, while their legs still run the way they mean to go. Jumps, dodges, attacks that lunge and
    /// every knockback are untouched. Read by the movement patches (<c>IceSlipPatch</c>, <c>IceFrictionPatch</c>) for
    /// the local player only.
    /// </summary>
    internal static class IceFooting
    {
        /// <summary>
        /// The physics step a grip share is stated for, so the slide is the same at any physics rate.
        /// </summary>
        private const float ReferenceStep = 0.02f;

        /// <summary>The share of their normal grip the local player has, 0.01 to 1.</summary>
        public static float Grip { get; private set; } = 1f;

        /// <summary>
        /// True while the local player stands on ice: the one check the movement patches make for everyone else.
        /// </summary>
        public static bool Slipping => Grip < 1f;

        public static void Set(float grip) => Grip = Mathf.Clamp(grip, 0.01f, 1f);

        public static void Clear() => Grip = 1f;

        /// <summary>
        /// Lets only the grip's share of this step's push land: <paramref name="wanted"/> is the velocity the game is
        /// about to give the body, <paramref name="moving"/> the velocity it has. Across the ground only; falling is
        /// the game's.
        /// </summary>
        public static void Slide(ref Vector3 wanted, Vector3 moving)
        {
            float share = 1f - Mathf.Pow(1f - Grip, Time.fixedDeltaTime / ReferenceStep);
            wanted.x = moving.x + (wanted.x - moving.x) * share;
            wanted.z = moving.z + (wanted.z - moving.z) * share;
        }

        /// <summary>The player's feet hold the ground only by the grip's share of their friction.</summary>
        public static void Loosen(PhysicsMaterial material)
        {
            material.staticFriction *= Grip;
            material.dynamicFriction *= Grip;
            // multiplied by the ground's own, never lifted back up to it by Maximum
            material.frictionCombine = PhysicsMaterialCombine.Multiply;
        }
    }
}
