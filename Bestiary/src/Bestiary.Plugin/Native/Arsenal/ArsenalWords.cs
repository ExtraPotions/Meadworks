// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// The arsenal's words for the game's translation table: the skeletons' names, the bone weapons', the spine's and
    /// the bone arrows'. English for every language until translations exist. Added again after each language setup,
    /// which rebuilds the table.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    public static class ArsenalWords
    {
        private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            ["Dagger"] = "Its blade is the spine of a single vertebra, its grip a tail of small ones. Quick, quiet and cruel.",
            ["Sword"] = "A blade of bone with a spine running down its middle, a vertebra for a guard and more for a grip.",
            ["Axe"] = "A bone blade set in a vertebra, on a haft of spine. It bites flesh and wood alike.",
            ["Mace"] = "A knot of vertebrae on a haft of spine. Heavy enough to crack whatever it meets.",
            ["Spear"] = "Long bones for the lower shaft, a spine for the upper, a leaf of bone for a point.",
            ["Atgeir"] = "Half long bones, half spine, ending in a curved bone blade and a hooked fang. It keeps the dead at a distance.",
            ["Bow"] = "Ribs for limbs and a backbone for a grip. The dead draw it well enough; so can you.",
        };

        public static void Add(Localization localization, string? language = null)
        {
            foreach (ArsenalWeapon weapon in ArsenalWeapon.All)
            {
                Adapters.NativeWords.Add(localization, "enemy_" + weapon.EnemyWord, "Skeleton " + weapon.Title, language);
                Adapters.NativeWords.Add(localization, "item_" + weapon.Word, "Bone " + weapon.Key, language);
                Adapters.NativeWords.Add(localization, "item_" + weapon.Word + "_description", Descriptions[weapon.Key], language);
            }
            Adapters.NativeWords.Add(localization, "item_" + ArsenalItems.SpineWord, "Spine", language);
            Adapters.NativeWords.Add(localization, "item_" + ArsenalItems.SpineWord + "_description",
                "A length of an old spine, still hard as stone. The skeletons build their weapons around it.", language);
            Adapters.NativeWords.Add(localization, "item_" + ArsenalArrow.Word, "Bone Arrow", language);
            Adapters.NativeWords.Add(localization, "item_" + ArsenalArrow.Word + "_description",
                "A shaft of bone tipped with a sharpened vertebra. Better than wood, not as good as flint.", language);
        }

        private static void Postfix(Localization __instance, string language) =>
            SafeCall.Run("Localization.SetupLanguage arsenal words", () => Add(__instance, language));
    }
}
