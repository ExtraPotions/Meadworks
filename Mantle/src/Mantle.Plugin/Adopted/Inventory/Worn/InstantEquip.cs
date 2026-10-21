using HarmonyLib;
using Mantle.Core;

namespace Mantle.Worn
{
    /// <summary>
    /// Instant Equip: the player's equip toggle (a hotbar key, a right click in the inventory) puts the item on or takes
    /// it off at once, as the game does for an item whose equip time is 0, instead of queueing the equipping bar. During
    /// an attack the game's toggle runs (it does nothing then). Runs on the player's own machine; the equipment shows on
    /// every peer the game's way.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.ToggleEquipped))]
    public static class InstantEquip
    {
        [HarmonyPrefix]
        public static bool Prefix(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!InventorySettings.Enabled.Value || !InventorySettings.InstantEquip.Value || item == null || !item.IsEquipable()
                || __instance.InAttack() || item.m_shared.m_equipDuration <= 0f)
                return true;
            if (__instance.IsItemEquiped(item))
                __instance.UnequipItem(item);
            else
                __instance.EquipItem(item);
            __result = true;
            return false;
        }
    }
}
