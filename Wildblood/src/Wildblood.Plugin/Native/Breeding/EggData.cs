// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Globalization;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Breeding
{
    /// <summary>
    /// A laid egg's inherited traits. They live in the egg item's own custom data, which the game saves with the item
    /// wherever it goes - on the ground, in a player's inventory, in a chest - so an egg carried to a warm spot still
    /// hatches the chick its parents made. The stars are also written as the egg's quality, the way the game already
    /// passes a parent's level to an egg, which keeps eggs of different stars from stacking. A stack keeps the custom
    /// data of the egg it was stacked onto; the game will not hatch a stack anyway.
    /// </summary>
    internal static class EggData
    {
        /// <summary>At the birth, before the egg's first save: the game persists what is written here on its own.</summary>
        public static void Write(ItemDrop egg, Lineage.Newborn born)
        {
            CreatureTraits t = born.Traits;
            egg.m_itemData.m_customData[TraitKeys.EggTraits] = string.Join(",",
                t.Stars.ToString(CultureInfo.InvariantCulture), t.Mask.ToString(CultureInfo.InvariantCulture),
                ((int)born.Biome).ToString(CultureInfo.InvariantCulture));
            if (!RuleState.Active.KeepsLevel(t.Stars, isBoss: false))
            {
                egg.SetQuality(t.Stars + 1); // with this mod's stars off the game's quality, the parent's level, stays
            }
            if (Log.Diagnostics)
            {
                Log.Diag($"breeding: egg laid with {t.Stars} stars, mask {t.Mask}");
            }
        }

        /// <summary>True when the egg carries traits from this mod; an egg from anywhere else hatches a wild roll.</summary>
        public static bool Has(ItemDrop.ItemData egg) => egg.m_customData.ContainsKey(TraitKeys.EggTraits);

        /// <summary>The newborn this egg hatches, or null when it carries no traits or they cannot be read. The hover
        /// line keeps what it reads (EggText), so this runs once per change; an unreadable egg simply hatches wild.</summary>
        public static Lineage.Newborn? Read(ItemDrop.ItemData egg)
        {
            if (!egg.m_customData.TryGetValue(TraitKeys.EggTraits, out string text))
            {
                return null;
            }
            string[] parts = text.Split(',');
            if (parts.Length < 3 || !Int(parts[0], out int stars) || !Int(parts[1], out int mask)
                || !Int(parts[2], out int biome))
            {
                if (Log.Diagnostics)
                {
                    Log.Diag($"breeding: an egg's traits '{text}' could not be read");
                }
                return null;
            }
            return new Lineage.Newborn(new CreatureTraits(stars, mask), (Heightmap.Biome)biome);
        }

        private static bool Int(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
    }
}
