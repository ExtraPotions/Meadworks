// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; one Ascension Rune
// gives every step back (2026-10-19).
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// The rune a Magic or Rare item may give back when another mod salvages it (OpenKeep's Salvage asks through the
    /// API): the first enabled promote rune that works on the rarity below the item's, so the Ascension Rune from an
    /// Uncommon, Rare or Epic item, at <see cref="Chance"/>. None for a Common or Legendary item (no rune raises Epic), an
    /// unknown rarity, a disabled rune or while <c>Runes from salvage</c> is off. The salvaging mod rolls the chance.
    /// </summary>
    internal static class SalvageRunes
    {
        /// <summary>The chance, 0-1, that a salvage gives the rune back.</summary>
        public const float Chance = 0.25f;

        /// <summary>The rune's prefab name (<c>ECF_Ascension</c>), or null when the item gives none back.</summary>
        public static string? PrefabFor(ItemDrop.ItemData? item)
        {
            if (item == null || !ModSettings.SalvageRunes.Value)
            {
                return null;
            }
            ItemState state = ItemState.Read(item);
            if (!state.IsMagic || state.Rarity == null)
            {
                return null;
            }
            StoneDef? rune = RaisedBy(ActiveRules.Current.Economy, state.Rarity);
            return rune != null && StonePrefabs.IsRegistered(rune.Prefab) ? rune.Prefab : null;
        }

        /// <summary><see cref="Chance"/> when the item gives a rune back, else 0.</summary>
        public static float ChanceFor(ItemDrop.ItemData? item) => PrefabFor(item) != null ? Chance : 0f;

        /// <summary>The first enabled promote rune that works on the rarity below <paramref name="rarity"/>.</summary>
        private static StoneDef? RaisedBy(EconomyRules economy, RarityDef rarity)
        {
            RarityDef? below = economy.Previous(rarity);
            if (below == null)
            {
                return null;
            }
            foreach (StoneDef rune in economy.Stones)
            {
                if (rune.Enabled && rune.Verb == StoneVerb.Promote && rune.AppliesToRarity(below.Id))
                {
                    return rune;
                }
            }
            return null;
        }
    }
}
