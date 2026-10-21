// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The Devouring creatures loaded on this machine, by creature. The game asks <c>BaseAI.IsEnemy</c> for every pair of
    /// characters an AI weighs, many times a second on whichever machine runs the AI - the dedicated server included - so
    /// <see cref="Patches.EnemyPatch"/> asks here instead of searching each creature's components: no work at all while no
    /// devourer is loaded, one lookup otherwise. Filled on every machine by <see cref="EliteController"/> as it applies
    /// the traits it loaded (a creature's mutations never change after that), and emptied as the controller is destroyed
    /// with its creature.
    /// </summary>
    public static class Devourers
    {
        private static readonly Dictionary<Character, EliteController> ByCreature =
            new Dictionary<Character, EliteController>();

        private static readonly Dictionary<Character, DevourBehaviour> Behaviours =
            new Dictionary<Character, DevourBehaviour>();

        /// <summary>True while any devourer is loaded here.</summary>
        public static bool Any => ByCreature.Count > 0;

        /// <summary>The resolved controller of a loaded Devouring creature, or null for every other character.</summary>
        public static EliteController? Of(Character character) =>
            ByCreature.Count > 0 && character is not null && ByCreature.TryGetValue(character, out EliteController devourer)
                && devourer.Ready && devourer.Traits.Has(Mutation.Devouring) ? devourer : null;

        /// <summary>Lists the creature when it carries Devouring; called once its traits are applied.</summary>
        public static void Track(EliteController controller)
        {
            if (controller.Traits.Has(Mutation.Devouring))
            {
                ByCreature[controller.Creature] = controller;
            }
        }

        /// <summary>The devourer's hunting state, once its behaviour has started; null before that and for every other.</summary>
        public static DevourBehaviour? BehaviourOf(Character character) =>
            character is not null && Behaviours.TryGetValue(character, out DevourBehaviour behaviour) ? behaviour : null;

        /// <summary>From the behaviour's own Start, on every machine: the enmity patch reads it from here.</summary>
        public static void Bind(Character character, DevourBehaviour behaviour) => Behaviours[character] = behaviour;

        // `is not null` rather than Unity's ==: the creature may already read as destroyed here, and its entry must go.
        public static void Forget(Character character)
        {
            if (character is not null)
            {
                ByCreature.Remove(character);
                Behaviours.Remove(character);
            }
        }
    }
}
