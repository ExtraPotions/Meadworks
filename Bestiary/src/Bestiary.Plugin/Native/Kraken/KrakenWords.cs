// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The kraken's words for the game's translation table: its name, the ink's status effect and its loot. English for
    /// every language until translations exist. Added again after each language setup, which rebuilds the table.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    public static class KrakenWords
    {
        public static void Add(Localization localization, string? language = null)
        {
            Adapters.NativeWords.Add(localization, "enemy_ecp_kraken", "Kraken", language);
            Adapters.NativeWords.Add(localization, "se_ecp_inked", "Inked", language);
            Adapters.NativeWords.Add(localization, "se_ecp_inked_tooltip", "Ink in your eyes.", language);
            Loot(localization, language);
        }

        private static void Loot(Localization localization, string? language)
        {
            Adapters.NativeWords.Add(localization, "item_ecp_krakenbeak", "Kraken beak", language);
            Adapters.NativeWords.Add(localization, "item_ecp_krakenbeak_description",
                "The kraken's hooked beak: dark horn with an amber edge that could shear through a hull. A smith could set it in a shield.", language);
            Adapters.NativeWords.Add(localization, "item_ecp_krakenmeat", "Raw kraken tentacle", language);
            Adapters.NativeWords.Add(localization, "item_ecp_krakenmeat_description", "A thick cut of kraken arm, suckers and all. Cook it on an iron cooking station.", language);
            Adapters.NativeWords.Add(localization, "item_ecp_krakenmeatcooked", "Cooked kraken tentacle", language);
            Adapters.NativeWords.Add(localization, "item_ecp_krakenmeatcooked_description", "Seared tentacle, rich and chewy. Keeps a crew rowing through the longest night.", language);
            Adapters.NativeWords.Add(localization, "item_ecp_shieldkraken", "Kraken shield", language);
            Adapters.NativeWords.Add(localization, "item_ecp_shieldkraken_description",
                "Fine wood and silver bound round the kraken's beak. Parry a blow with it and the beak bites back.", language);
        }

        private static void Postfix(Localization __instance, string language) =>
            SafeCall.Run("Localization.SetupLanguage kraken words", () => Add(__instance, language));
    }
}
