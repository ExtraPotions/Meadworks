// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Patches;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The moments a player's gear tier can change, on that player's own client: its equipment is set up again (every
    /// equip, unequip and load passes through <c>Humanoid.SetupEquipment</c>), it comes to know a new item, and it
    /// spawns (known materials load without passing <c>AddKnownItem</c>). Each publishes the tier when it differs
    /// (<see cref="GearTier.Publish"/>). None runs per frame, and every other creature or player costs one reference
    /// test. A failure is reported and swallowed: the game's own step always happens.
    /// </summary>
    [HarmonyPatch]
    internal static class GearTierPatch
    {
        [HarmonyPatch(typeof(Humanoid), "SetupEquipment")]
        [HarmonyPostfix]
        private static void AfterEquipment(Humanoid __instance)
        {
            if (ReferenceEquals(__instance, Player.m_localPlayer))
            {
                SafeCall.Run("raid gear tier (equipment)", static p => GearTier.Publish(p), Player.m_localPlayer);
            }
        }

        // The game calls AddKnownItem for every item in the inventory on every inventory change, so only an item this
        // player did not know yet (one hash lookup to find out) is worth working the tier out again.
        [HarmonyPatch(typeof(Player), nameof(Player.AddKnownItem))]
        [HarmonyPrefix]
        private static void BeforeKnownItem(Player __instance, ItemDrop.ItemData item, out bool __state) =>
            __state = ReferenceEquals(__instance, Player.m_localPlayer) && item?.m_shared != null
                && !__instance.m_knownMaterial.Contains(item.m_shared.m_name);

        [HarmonyPatch(typeof(Player), nameof(Player.AddKnownItem))]
        [HarmonyPostfix]
        private static void AfterKnownItem(Player __instance, bool __state)
        {
            if (__state)
            {
                SafeCall.Run("raid gear tier (known item)", static p => GearTier.Publish(p), __instance);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        private static void AfterSpawned(Player __instance) =>
            SafeCall.Run("raid gear tier (spawned)", static p => GearTier.Publish(p), __instance);
    }
}
