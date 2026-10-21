using System;
using HarmonyLib;

namespace Burrow.Reach
{
    /// <summary>
    /// An empty fermenter takes a base mead through <c>Fermenter.Interact</c>. When the inventory has none and the
    /// barrel is under a roof and sheltered (the game's own conditions), one unit is borrowed before the game looks.
    /// </summary>
    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.Interact))]
    public static class FermenterPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Fermenter __instance, Humanoid user, bool hold, ref StationFeed.Loan __state, ref bool __result)
        {
            __state = null;
            if (hold || !StationFeed.Wanted(user, __instance) || !PrivateArea.CheckAccess(__instance.transform.position, 0f, false))
                return true;
            if (__instance.GetContent() != 0)
                return true;
            Func<ItemDrop.ItemData, bool> accepts = StationAccepts.FermenterBase(__instance);
            if (StationFeed.PullHeld)
            {
                __result = StationFeed.Pull(user, accepts);
                return false;
            }
            __instance.UpdateCover(0f, true);
            if (!__instance.m_hasRoof || __instance.m_exposed || __instance.FindCookableItem(user.GetInventory()) != null)
                return true;
            __state = StationFeed.Borrow(user, accepts);
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Humanoid user, StationFeed.Loan __state)
        {
            StationFeed.Settle(user, __state);
        }
    }
}
