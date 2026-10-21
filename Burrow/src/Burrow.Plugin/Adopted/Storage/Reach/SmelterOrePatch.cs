using System;
using HarmonyLib;

namespace Burrow.Reach
{
    /// <summary>
    /// The smelter's add ore switch (<c>Smelter.OnAddOre</c>, also kilns, blast furnaces, spinning wheels,
    /// windmills and sap extractors). With an empty hand and no ore in the inventory one unit is borrowed from
    /// storage before the game looks; the game's own checks (allowed item, queue cap) and RPC do the rest.
    /// </summary>
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddOre))]
    public static class SmelterOrePatch
    {
        /// <summary>
        /// One call's state. The game assigns the ore it finds to its own item parameter, so the postfix cannot tell
        /// from that parameter whether the switch was used with an empty hand; the prefix records it (Burrow change).
        /// </summary>
        public sealed class Call
        {
            public StationFeed.Loan Loan;
            public bool EmptyHand;
        }

        [HarmonyPrefix]
        public static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, ref Call __state, ref bool __result)
        {
            __state = new Call { EmptyHand = item == null };
            if (item != null || !StationFeed.Wanted(user, __instance))
                return true;
            Func<ItemDrop.ItemData, bool> accepts = StationAccepts.SmelterOre(__instance);
            if (StationFeed.PullHeld)
            {
                __result = StationFeed.Pull(user, accepts);
                return false;
            }
            if (__instance.GetQueueSize() >= __instance.m_maxOre || __instance.FindCookableItem(user.GetInventory()) != null)
                return true;
            __state.Loan = StationFeed.Borrow(user, accepts);
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Smelter __instance, Switch sw, Humanoid user, Call __state, bool __result)
        {
            StationFeed.Settle(user, __state?.Loan);
            if (__state != null && __state.EmptyHand && __result && StationFeed.Wanted(user, __instance))
                StationFeed.Fill(__instance.m_nview, __instance.m_maxOre, () => __instance.OnAddOre(sw, user, null));
        }
    }
}
