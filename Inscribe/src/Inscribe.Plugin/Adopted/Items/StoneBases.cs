// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>The look family a rune prefab is cloned from (prefabs.md section 4a).</summary>
    internal enum StoneGroup
    {
        Ascension,
        Manipulation,
        Risk,
        Gem,
        Tool,
    }

    /// <summary>
    /// Which vanilla item each rune prefab is cloned from (prefabs.md section 4a, DECISIONS.md PRF-1). The base names
    /// live in the game's asset bundles, not in code, so none is verified here: each group tries its proposed base,
    /// then its alternates, then any candidate of any group (with a warning), and a missing base never throws.
    /// Runs on every peer, identically.
    /// </summary>
    internal static class StoneBases
    {
        private static readonly string[] Ascension = { "Ruby", "Amber", "AmberPearl" };
        private static readonly string[] Manipulation = { "Crystal", "DragonTear", "Thunderstone" };
        private static readonly string[] Risk = { "SurtlingCore", "BlackCore" };
        private static readonly string[] Gem = { "Ruby", "Amber", "AmberPearl" };
        private static readonly string[] Tool = { "IronNails", "BronzeNails" };

        // Indexed by StoneGroup: keep in the enum's order.
        private static readonly string[][] AllGroups = { Ascension, Manipulation, Risk, Gem, Tool };

        /// <summary>
        /// The look of a rune: the two that change the rarity a gem, the Serpent a core, the rest a crystal; the gems a
        /// tinted Ruby, the Dvergr Chisel tinted nails (until they have models of their own).
        /// </summary>
        public static StoneGroup GroupOf(string runeId)
        {
            if (StoneCatalog.IsSocketStone(runeId))
            {
                return runeId == StoneCatalog.ChiselId ? StoneGroup.Tool : StoneGroup.Gem;
            }
            switch (runeId)
            {
                case "awakening":
                case "ascension":
                    return StoneGroup.Ascension;
                case "serpent":
                    return StoneGroup.Risk;
                default:
                    return StoneGroup.Manipulation;
            }
        }

        /// <summary>Whether any candidate base of any group is in the given lists (false on the main menu's empty first Awake).</summary>
        public static bool AnyPresent(IList<GameObject>? first, IList<GameObject>? second)
        {
            foreach (string[] group in AllGroups)
            {
                foreach (string name in group)
                {
                    if (Find(first, second, name) != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>The base prefab for a group: its own candidates in order, else any candidate at all. Null only when none exists.</summary>
        public static GameObject? Resolve(StoneGroup group, IList<GameObject>? first, IList<GameObject>? second)
        {
            string[] own = AllGroups[(int)group];
            GameObject? found = FirstOf(own, first, second);
            if (found != null)
            {
                if (found.name != own[0])
                {
                    Log.Warn($"rune base {own[0]} not found for the {group} runes; using {found.name}");
                }
                return found;
            }
            foreach (string[] other in AllGroups)
            {
                found = FirstOf(other, first, second);
                if (found != null)
                {
                    Log.Warn($"no base of the {group} group ({string.Join(", ", own)}) exists; using {found.name}");
                    return found;
                }
            }
            return null;
        }

        private static GameObject? FirstOf(string[] names, IList<GameObject>? first, IList<GameObject>? second)
        {
            foreach (string name in names)
            {
                GameObject? found = Find(first, second, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>A game item (an ItemDrop on it) by name from either list, or null.</summary>
        public static GameObject? Find(IList<GameObject>? first, IList<GameObject>? second, string name) =>
            FindIn(first, name) ?? FindIn(second, name);

        // Only real items qualify: the object must carry an ItemDrop to clone the item data from.
        private static GameObject? FindIn(IList<GameObject>? list, string name)
        {
            if (list == null)
            {
                return null;
            }
            for (int i = 0; i < list.Count; i++)
            {
                GameObject go = list[i];
                if (go != null && go.name == name && go.GetComponent<ItemDrop>() != null)
                {
                    return go;
                }
            }
            return null;
        }
    }
}
