// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The Crypt Executioner's words for the game's translation table: its name, its attacks' (never shown), and the
    /// players' greataxe and axehead. English for every language until translations exist. Added again after each
    /// language setup, which rebuilds the table.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    public static class HeadsmanWords
    {
        public static void Add(Localization localization, string? language = null)
        {
            Adapters.NativeWords.Add(localization, "enemy_ecp_headsman", "Crypt Executioner", language);
            foreach (string attack in new[] { "slam", "scrape", "spin", "hurl", "spinthrow", "rear" })
            {
                Adapters.NativeWords.Add(localization, "item_ecp_headsman_" + attack, "Executioner's axe", language);
            }
            Adapters.NativeWords.Add(localization, "item_" + GreataxeItems.AxeWord, "Executioner's Greataxe", language);
            Adapters.NativeWords.Add(localization, "item_" + GreataxeItems.AxeWord + "_description",
                "The Crypt Executioner's greataxe, its haft strung from vertebrae. It slashes, whirls all the way round, then "
                + "falls from overhead; the last blow is slow to recover from.", language);
            Adapters.NativeWords.Add(localization, "item_" + GreataxeItems.HeadWord, "Executioner's axehead", language);
            Adapters.NativeWords.Add(localization, "item_" + GreataxeItems.HeadWord + "_description",
                "The bone head of the Crypt Executioner's axe. Set on a haft of spines at the workbench, it swings again.", language);
        }

        private static void Postfix(Localization __instance, string language) =>
            SafeCall.Run("Localization.SetupLanguage crypt executioner words", () => Add(__instance, language));
    }
}
