// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// Every creature loaded on this machine whose traits have resolved, by creature, for the per-frame paths that start
    /// from a Character (the nameplate asks for its hover name every frame it shows): one lookup instead of a component
    /// search. Filled by <see cref="EliteController"/> as it applies the traits it loaded, and emptied as the controller
    /// is destroyed with its creature.
    /// </summary>
    public static class ReadyElites
    {
        private static readonly Dictionary<Character, EliteController> ByCreature = new Dictionary<Character, EliteController>();

        /// <summary>The resolved controller of a creature, or null for a player and for one not resolved yet.</summary>
        public static EliteController? Of(Character character) =>
            character is not null && ByCreature.TryGetValue(character, out EliteController controller) && controller.Ready ? controller : null;

        public static void Track(EliteController controller) => ByCreature[controller.Creature] = controller;

        // `is not null` rather than Unity's ==: the creature may already read as destroyed here, and its entry must go.
        public static void Forget(Character character)
        {
            if (character is not null)
            {
                ByCreature.Remove(character);
            }
        }
    }
}
