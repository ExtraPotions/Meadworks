// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The giant's words for the game's translation table: its name and its attacks'. English for every language until
    /// translations exist. Added again after each language setup, which rebuilds the table.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    public static class RimeGiantWords
    {
        public static void Add(Localization localization, string? language = null)
        {
            Adapters.NativeWords.Add(localization, "enemy_ecp_rimegiant", "Rime Giant", language);
            Adapters.NativeWords.Add(localization, "item_ecp_rimegiant_sweep", "Rime fist", language);
            Adapters.NativeWords.Add(localization, "item_ecp_rimegiant_slam", "Avalanche", language);
            Adapters.NativeWords.Add(localization, "item_ecp_rimegiant_throw", "Ice boulder", language);
        }

        private static void Postfix(Localization __instance, string language) =>
            SafeCall.Run("Localization.SetupLanguage rime giant words", () => Add(__instance, language));
    }
}
