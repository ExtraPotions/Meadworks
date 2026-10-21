// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Loot profiles other mods set for their creatures through the API (<c>SetCreatureLoot</c>, api.md section 5): a
    /// boss entry and a creature entry per prefab, in the shapes of <c>drops.bosses</c> and <c>drops.creatures</c>.
    /// <see cref="CreatureProfiles"/> reads each right under the economy YAML's map of the same kind: a YAML entry for
    /// the prefab wins whole. Code, not data: every peer runs the same mods and sets the same profiles. Main thread only.
    /// </summary>
    internal static class CodeLoot
    {
        private static readonly Dictionary<string, BossDrop> Bosses = new Dictionary<string, BossDrop>(StringComparer.Ordinal);
        private static readonly Dictionary<string, CreatureDrop> Creatures = new Dictionary<string, CreatureDrop>(StringComparer.Ordinal);

        /// <summary>Replaces the prefab's code entries (a null removes that kind) and drops the cached profiles.</summary>
        public static void Set(string prefab, BossDrop? boss, CreatureDrop? creature)
        {
            Put(Bosses, prefab, boss);
            Put(Creatures, prefab, creature);
            CreatureProfiles.Clear();
        }

        /// <summary>The YAML's boss entry for the prefab, else the code's, else null.</summary>
        public static BossDrop? Boss(DropRules drops, string prefab) =>
            drops.Bosses.TryGetValue(prefab, out BossDrop yaml) ? yaml : Bosses.TryGetValue(prefab, out BossDrop code) ? code : null;

        /// <summary>The YAML's creature entry for the prefab, else the code's, else null.</summary>
        public static CreatureDrop? Creature(DropRules drops, string prefab) =>
            drops.Creatures.TryGetValue(prefab, out CreatureDrop yaml) ? yaml : Creatures.TryGetValue(prefab, out CreatureDrop code) ? code : null;

        private static void Put<T>(Dictionary<string, T> map, string prefab, T? value) where T : class
        {
            if (value == null)
            {
                map.Remove(prefab);
            }
            else
            {
                map[prefab] = value;
            }
        }
    }
}
