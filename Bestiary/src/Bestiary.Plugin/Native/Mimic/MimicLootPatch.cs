// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// Adds a mimic's chest loot to its drop list, after every other change to the list (Elite Creatures Reborn's loot rules included, when it is installed), so
    /// the chest's rows arrive exactly as the chest would have held them.
    /// </summary>
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    public static class MimicLootPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result) =>
            SafeCall.Run("CharacterDrop.GenerateDropList mimic", static (drop, list) => MimicLoot.AddChestLoot(drop, list), __instance, __result);
    }
}
