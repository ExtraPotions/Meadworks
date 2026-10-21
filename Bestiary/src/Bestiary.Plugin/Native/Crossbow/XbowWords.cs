// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// The crossbowman's words for the game's translation table: its name, its crossbow's, and the players' Bone
    /// Crossbow's. English for every language until translations exist. Added again after each language setup, which
    /// rebuilds the table.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    public static class XbowWords
    {
        public static void Add(Localization localization, string? language = null)
        {
            Adapters.NativeWords.Add(localization, "enemy_ecp_skeletoncrossbowman", "Skeleton Crossbowman", language);
            Adapters.NativeWords.Add(localization, "item_ecp_skeletoncrossbow", "Crossbow", language);
            Adapters.NativeWords.Add(localization, "item_" + XbowItem.Word, "Bone Crossbow", language);
            Adapters.NativeWords.Add(localization, "item_" + XbowItem.Word + "_description",
                "A crossbow of bones, made the way the dead make theirs: a femur for a stock, a spine for a tiller, ribs for a "
                + "prod. It hits like a club, whatever bolt it looses.", language);
            Adapters.NativeWords.Add(localization, "item_" + XbowBolts.Word, "Blunted Bone Bolt", language);
            Adapters.NativeWords.Add(localization, "item_" + XbowBolts.Word + "_description",
                "A bolt of bone with its head ground blunt and no fletching, for the bone crossbow. It breaks bones rather than "
                + "piercing them.", language);
        }

        private static void Postfix(Localization __instance, string language) =>
            SafeCall.Run("Localization.SetupLanguage crossbowman words", () => Add(__instance, language));
    }
}
