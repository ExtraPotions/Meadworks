// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Writes the economy YAML's item values into every rune prefab (prefabs.md section 2 "YAML on top",
    /// economy-yaml.md section 4): name, description, max stack, item weight, plus teleportable and no trade value.
    /// Runs on every peer after the prefabs are built and on every rules change. Every live copy of a rune shares its
    /// prefab's shared data (<see cref="StonePrefabs.LinkShared"/>), so this reaches existing stacks too; a rune the
    /// YAML disables keeps its prefab and built-in values, so its stacks survive (RC-3).
    /// </summary>
    internal static class StoneItemData
    {
        private static readonly StoneDef Defaults = new StoneDef();

        public static void ApplyAll(RuleSet rules)
        {
            StoneTints.Rebuild(rules.Economy);
            foreach (StoneEntry entry in StonePrefabs.All)
            {
                Apply(entry, rules.Economy.StoneForPrefab(entry.PrefabName));
                StoneVisuals.Apply(entry);
            }
        }

        private static void Apply(StoneEntry entry, StoneDef? def)
        {
            ItemDrop.ItemData.SharedData shared = entry.Shared;
            shared.m_name = def?.Name ?? "$ecf_stone_" + entry.BuiltInId;
            shared.m_description = DescriptionOf(entry, def);
            shared.m_maxStackSize = def?.Stack ?? Defaults.Stack;
            shared.m_weight = def?.ItemWeight ?? Defaults.ItemWeight;
            shared.m_teleportable = true;
            shared.m_value = 0;
        }

        // The definition's description (economy-yaml.md section 4); the default key only when a word exists for it,
        // so a rune without a description shows none rather than a raw key.
        private static string DescriptionOf(StoneEntry entry, StoneDef? def)
        {
            string key = "ecf_stone_" + entry.BuiltInId + "_desc";
            string text = def?.Description ?? "$" + key;
            return text == "$" + key && !Words.Has(key) ? "" : text;
        }
    }
}
