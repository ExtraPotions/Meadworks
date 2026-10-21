// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from OpenKeep 4.1.0 9da28eb58adbe065be1e4baef779a448acd38e0f,
// OpenKeep/OpenKeep/src/Recipes/TypingStill.cs. Copyright (C) 2026 Gerald Glasgow.
// GNU GPL version 3 or later; full notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespace; the focus test is the Crucible kit's text fields (recipe search, batch amount).
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// The player stands still while typing in Kindle's fields (the recipe search, the batch amount): the game lets the
    /// player walk with the inventory open and reads W, A, S and D through PlayerController.TakeInput, which already
    /// refuses for the game's own text fields (chat, console, sign text, the build menu's search) but knows nothing of a
    /// mod's. Typing "wood" walked the player forward (OpenKeep 4.0.0 fix, 2026-10-07).
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), "TakeInput")]
    internal static class TypingStill
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (__result && KitTextField.AnyFocused)
                __result = false;
        }
    }
}
