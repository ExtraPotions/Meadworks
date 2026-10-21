// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>Icons and item data the window shows: the runes', the essences' (game items), and a rune to pay with.</summary>
    internal static class TableIcons
    {
        public static Sprite? Rune(string runeId) => StonePrefabs.Get(runeId)?.GetComponent<ItemDrop>()?.m_itemData.GetIcon();

        /// <summary>The pure essence item's icon.</summary>
        public static Sprite? EssenceItem()
        {
            GameObject? prefab = Tables.EssenceItem.Prefab;
            return prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData.GetIcon() : null;
        }

        /// <summary>The essence's own icon (<see cref="EssenceIcons"/>), else its game item's.</summary>
        public static Sprite? Essence(Essence essence) => EssenceIcons.Of(essence);

        /// <summary>The essence's icon in greyscale, while it cannot be chosen; null without an embedded icon.</summary>
        public static Sprite? EssenceGrey(Essence essence) => EssenceIcons.Grey(essence);

        /// <summary>The rune's definition in the rules in force (the server's while it binds), or null.</summary>
        public static StoneDef? Def(string runeId) => ActiveRules.Current.Economy.Stone(runeId);

        /// <summary>A copy of the rune prefab's item data, one piece, linked to its prefab: what a table use pays with.</summary>
        public static ItemDrop.ItemData? RuneItem(string runeId)
        {
            GameObject? prefab = StonePrefabs.Get(runeId);
            ItemDrop? drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return null;
            }
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = 1;
            return item;
        }

        /// <summary>
        /// Whether an essence works with the rune: the Ascension Rune only, whose new inscription the essence then
        /// guarantees (user decision 2026-10-07); every other rune leaves the essences greyed out.
        /// </summary>
        public static bool Steers(StoneDef? def) => def != null && def.Id == AscensionId;

        public const string AscensionId = "ascension";
    }
}
