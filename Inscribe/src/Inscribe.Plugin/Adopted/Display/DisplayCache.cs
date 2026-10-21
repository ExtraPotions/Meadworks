// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Runtime.CompilerServices;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Built display text per item state (display.md section 3): the colored name topic and the tooltip block per
    /// (detail level, dormant switch). Keyed weakly on the immutable <see cref="ItemState"/> instance, which the parse
    /// cache replaces whenever the item's data, the rules generation or a write changes it. A global generation,
    /// bumped on <c>ItemStateCache.Written</c>, <c>ActiveRules.RulesChanged</c> and every language setup, drops every
    /// entry at once. A hit is one table lookup, one int compare and one array read: no allocation per frame.
    /// Main thread only (every caller is a UI or hover path on the viewing client).
    /// </summary>
    internal static class DisplayCache
    {
        private sealed class Entry
        {
            public int Generation = -1;
            public bool HasTopic;
            public string? Topic;
            public string? NameToken;
            public readonly string?[] Blocks = new string?[6];
        }

        private static readonly ConditionalWeakTable<ItemState, Entry> Table = new ConditionalWeakTable<ItemState, Entry>();
        private static int _generation;
        internal static int Generation => _generation;

        /// <summary>Drops every built text; the next draw rebuilds from the current rules, words and palette.</summary>
        public static void Invalidate() => _generation++;

        /// <summary>
        /// The tooltip title with the rarity color (<c>&lt;color=#hex&gt;$item_x&lt;/color&gt;</c>, unlocalized like
        /// vanilla's), or null when the name stays vanilla: Common, unknown rarity, or <c>Colored item names</c> off.
        /// </summary>
        public static string? Topic(ItemState state, ItemDrop.ItemData item)
        {
            if (!ModSettings.ColoredItemNames.Value || state.Rarity == null)
            {
                return null;
            }
            Entry entry = Current(state);
            if (!entry.HasTopic || entry.NameToken != item.m_shared.m_name)
            {
                string? tag = RarityPalette.NameTag(state.Rarity);
                entry.Topic = tag == null ? null : tag + item.m_shared.m_name + RarityPalette.Close;
                entry.NameToken = item.m_shared.m_name;
                entry.HasTopic = true;
            }
            return entry.Topic;
        }

        /// <summary>The localized affix block for the player's current detail preferences ("" when nothing to show).</summary>
        public static string Block(ItemState state, ItemDrop.ItemData item)
        {
            TooltipDetail detail = ModSettings.TooltipDetailLevel.Value;
            bool dormant = ModSettings.ShowDormantAffixes.Value;
            int slot = ((int)detail % 3) * 2 + (dormant ? 1 : 0);
            Entry entry = Current(state);
            return entry.Blocks[slot] ??= TooltipBlock.Build(state, item, detail, dormant);
        }

        private static Entry Current(ItemState state)
        {
            Entry entry = Table.GetOrCreateValue(state);
            if (entry.Generation != _generation)
            {
                entry.Generation = _generation;
                entry.HasTopic = false;
                entry.Topic = null;
                System.Array.Clear(entry.Blocks, 0, entry.Blocks.Length);
            }
            return entry;
        }
    }
}
