using HarmonyLib;
using Burrow.Core;

namespace Burrow.Core
{
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.FindEmptySlot))]
    public static class InventoryOrder
    {
        [HarmonyPrefix]
        public static bool Prefix(Inventory __instance, ref Vector2i __result)
        {
            bool player = Player.m_localPlayer != null && __instance == Player.m_localPlayer.GetInventory();
            if (player && PackPanelLink.LaysOutInventory) return true;
            int rows = __instance.GetHeight();
            for (int row = 0; row < rows; row++)
            {
                int y = player ? (row + 1) % rows : row;
                for (int x = 0; x < __instance.GetWidth(); x++)
                    if (__instance.GetItemAt(x, y) == null) { __result = new Vector2i(x, y); return false; }
            }
            __result = new Vector2i(-1, -1);
            return false;
        }
    }
}
