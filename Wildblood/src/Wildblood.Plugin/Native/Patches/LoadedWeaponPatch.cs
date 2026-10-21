// SPDX-License-Identifier: GPL-3.0-or-later
// Loaded crossbow continuity adapted from ECR 51abb440b89313d2130519116b83f9c65a7c11de
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Changed by expDARE, 2026: existing Wildblood availability and guarded native callbacks.
using System;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
    public static class LoadedWeaponUnequipPatch
    {
        private static void Prefix(Humanoid __instance, ItemDrop.ItemData item)
        {
            try { LoadedWeapons.Unequipping(__instance, item); }
            catch (Exception error) { Guard.Report(error, "loaded crossbow unequip"); }
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    public static class LoadedWeaponEquipPatch
    {
        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result) return;
            try { LoadedWeapons.Equipped(__instance, item); }
            catch (Exception error) { Guard.Report(error, "loaded crossbow equip"); }
        }
    }
}
