// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the host's switch for rings, belts and trinkets (batch 3), as a magic-base veto read live.
using ExtraPotions.Inscribe.Config;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// <c>1 - General / Accessory Rarity</c> off: items of the <c>ring</c>, <c>belt</c> and <c>trinket</c> classes are no magic
    /// base, so they neither drop rolled nor take runes. Item data already on them is untouched and keeps applying.
    /// </summary>
    internal static class AccessoryRarity
    {
        private const string FilterId = "inscribe.accessory_rarity";

        public static void Install() => Adopted.Items.MagicBaseFilters.Filters.Set(FilterId, Allows);

        private static bool Allows(ItemDrop.ItemData item)
        {
            InscribeConfig? settings = Plugin.Settings;
            if (settings == null || settings.Get(InscribeSettings.General.AccessoryRarity)) return true;
            string? cls = Adopted.Items.ItemClasses.Classify(item).ClassId ?? Adopted.Items.ItemClasses.ClassifyForRune(item).ClassId;
            return cls != "ring" && cls != "belt" && cls != "trinket";
        }
    }
}
