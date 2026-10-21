// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildblood.Adopted.Raids {
    [HarmonyPatch(typeof(CharacterDrop),"GenerateDropList")]
    static class RaidDropPatch {
        [HarmonyPriority(Priority.Last-1)]
        static void Postfix(CharacterDrop __instance,List<KeyValuePair<GameObject,int>> __result)=>RaidLoot.Apply(__instance,__result);
    }
}
