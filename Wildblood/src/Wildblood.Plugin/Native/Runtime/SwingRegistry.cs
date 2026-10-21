// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// The creatures loaded on this machine that do not animate at the game's own speed: a swing speed from stars or Mad
    /// other than 1, or a Tethered boss, whose speed moves with its pair's health and so is listed even while it is 1.
    /// <c>AnimSpeedPatch</c> runs for every character's animator each fixed step, players included, so everyone else
    /// costs it this one lookup. Filled on every machine by <see cref="EliteController"/> as it applies the traits it
    /// loaded from the ZDO, which it does once and never redoes (a hand-over or a rule reload changes nothing here), and
    /// emptied as the controller is destroyed with its creature.
    /// </summary>
    public static class SwingRegistry
    {
        private static readonly Dictionary<Character, EliteController> Swinging = new Dictionary<Character, EliteController>();

        /// <summary>The controller of a creature listed here, or null for a player and for every creature at its own speed.</summary>
        public static EliteController? For(Character character) =>
            character is not null && Swinging.TryGetValue(character, out EliteController controller) && controller.Ready ? controller : null;

        /// <summary>Lists the creature when its swing speed is not 1 or can change; called once its traits are applied.</summary>
        public static void Track(EliteController controller)
        {
            if (!Mathf.Approximately(controller.SwingSpeedFactor, 1f) || controller.Traits.HasAspect(Aspect.Tethered))
            {
                Swinging[controller.Creature] = controller;
            }
        }

        // `is not null` rather than Unity's ==: the creature may already read as destroyed here, and its entry must go.
        public static void Forget(Character character)
        {
            if (character is not null)
            {
                Swinging.Remove(character);
            }
        }
    }
}
