// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Runtime;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Applies the attack-speed part of star scaling and Mad, and Tethered's live boost as the pair's health draws apart
    /// (<see cref="TetherLink.SwingFactor"/>). The game resets a creature's animator speed to 1 each fixed
    /// step, but only while it is idle or moving - during an attack, a minor action, an emote or a stagger it leaves the
    /// value alone. So the scaled speed is written absolutely inside that same window and the attack inherits it for its
    /// whole swing; multiplying the live value would compound once per fixed step and run away. Runs on every machine
    /// (animation is local); the factor comes from synced traits and, for Tethered, from the pair's health in their ZDOs,
    /// so every client animates it the same. Tethered's part changes from step to step, which the absolute write takes
    /// as it comes: the fixed factor times the live one, never times the animator's current speed.
    /// It runs for every character's animator, players included, so it asks <see cref="SwingRegistry"/> first and leaves
    /// at once for anything at its own speed: one lookup, nothing allocated. Exceptions are reported the way
    /// <see cref="Guard.Run(string, Action)"/> reports them, inline so the step allocates no closure.
    /// </summary>
    [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.CustomFixedUpdate))]
    public static class AnimSpeedPatch
    {
        private static void Postfix(CharacterAnimEvent __instance)
        {
            EliteController? controller = SwingRegistry.For(__instance.m_character);
            if (controller is null)
            {
                return;
            }
            try
            {
                Adjust(__instance, controller);
            }
            catch (Exception e)
            {
                Guard.Report(e, "CharacterAnimEvent.CustomFixedUpdate");
                throw;
            }
        }

        private static void Adjust(CharacterAnimEvent animEvent, EliteController controller)
        {
            Character character = animEvent.m_character;
            Animator animator = animEvent.m_animator;
            float factor = controller.SwingSpeedFactor * TetherLink.SwingFactor(controller);
            if (animator == null || Mathf.Approximately(factor, 1f))
            {
                return;
            }
            // Mirrors the game's own reset condition. Outside it the animator speed belongs to the game - an attack's
            // authored speed, a freeze frame - and writing there would either fight it or compound onto itself.
            if (!character.InAttack() && !character.InMinorAction() && !character.InEmote() && character.CanMove())
            {
                animator.speed = factor;
            }
        }
    }
}
