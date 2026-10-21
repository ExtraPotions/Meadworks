// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-or-later
// Portions from EliteCreaturesReborn 3.21.0 (Gerald Glasgow), GPL-3.0-or-later, at 9da28eb58adbe065be1e4baef779a448acd38e0f:
// EliteCreaturesReborn/EliteCreaturesReborn/Patches/LoadedWeaponPatch.cs. Source notice retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace; ECR's SafeCall replaced by a guard that reports through Edge diagnostics.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using ExtraPotions.Edge.Secondaries.Adopted;
using HarmonyLib;

namespace ExtraPotions.Edge.Runtime.Equipment;

/// <summary>
/// Humanoid.UnequipItem forgets a player's loaded weapon (it calls ResetLoadedWeapon); the prefix notes the weapon
/// first, while the game still names it as loaded. A failure is reported and the game unequips as before.
/// </summary>
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
internal static class LoadedWeaponUnequipPatch
{
    private static void Prefix(Humanoid __instance, ItemDrop.ItemData item)
    {
        if (item != null)
        {
            LoadedWeaponGuard.Run("unequip", LoadedWeapons.Unequipping, __instance, item);
        }
    }
}

/// <summary>
/// Humanoid.EquipItem has drawn a weapon: one put away loaded is loaded again, before the game's next update would
/// queue a reload for it. A failure is reported and the weapon reloads as the game would.
/// </summary>
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
internal static class LoadedWeaponEquipPatch
{
    private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
    {
        if (__result && item != null)
        {
            LoadedWeaponGuard.Run("equip", LoadedWeapons.Equipped, __instance, item);
        }
    }
}

internal static class LoadedWeaponGuard
{
    private static bool reported;

    internal static void Run(string step, Action<Humanoid, ItemDrop.ItemData> action, Humanoid humanoid, ItemDrop.ItemData item)
    {
        try
        {
            action(humanoid, item);
        }
        catch (Exception exception)
        {
            if (reported) return;
            reported = true;
            RuleDiagnostics.Warning($"Keep Crossbow Loaded ({step}) failed; the game reloads as usual. {exception}");
        }
    }
}
