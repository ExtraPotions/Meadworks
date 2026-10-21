using HarmonyLib;
using Mantle.Backpacks;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Layout
{
    /// <summary>
    /// Base Carry Weight (the user's request): the game's limit is the player's <c>m_maxCarryWeight</c> (300) plus what
    /// status effects add (Megingjord, meads), times the world's carry weight modifier (<c>Player.GetMaxCarryWeight</c>).
    /// The postfix swaps the 300 for the setting, keeping the effects and the modifier, and adds the worn backpack's carry
    /// weight (<see cref="Backpack"/>; scaled by the modifier like the base). At 300 with no backpack, or with the
    /// master switch off, it does nothing, so a mod that changes <c>m_maxCarryWeight</c> itself keeps working. Every client works out its own player's limit from
    /// the synced value, so a dedicated server needs nothing.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
    public static class CarryWeight
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, ref float __result)
        {
            if (!InventorySettings.Enabled.Value)
                return;
            float setting = InventorySettings.BaseCarryWeight.Value;
            float swap = Mathf.Approximately(setting, InventorySettings.GameCarryWeight) ? 0f : setting - __instance.m_maxCarryWeight;
            float added = swap + BackpackSettings.Carry(Backpack.Worn(__instance));
            if (added != 0f)
                __result = Mathf.Max(0f, __result + added * Game.m_carryWeightRate);
        }
    }
}
