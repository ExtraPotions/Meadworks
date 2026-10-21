using System.Reflection;
using HarmonyLib;

namespace Mantle.Layout
{
    /// <summary>
    /// BiomeLords' Featherweight methods that size or fill the player's inventory, each patched only when BiomeLords is
    /// installed (<c>Prepare</c>). <c>SetHeight</c> (every resize: at spawn, when a blessing is granted or switched,
    /// before the game drops invalid items) becomes Mantle's layout (<see cref="BiomeLordsLink.TakeOver"/>).
    /// <c>EnsureExpanded</c> (raises the height before an item is added) and <c>FindExtraRowSlot</c> (a free cell in the
    /// rows under its base, which are Mantle's slots) do nothing while Mantle lays out the inventory: the
    /// blessing's rows are main rows there, so the game's own search finds them.
    /// </summary>
    public static class BiomeLordsPatches
    {
        [HarmonyPatch]
        public static class SetHeight
        {
            public static bool Prepare() => BiomeLordsLink.Method("SetHeight") != null;

            public static MethodBase TargetMethod() => BiomeLordsLink.Method("SetHeight");

            [HarmonyPrefix]
            public static bool Prefix(Player __0, int __1) => !BiomeLordsLink.TakeOver(__0, __1);
        }

        [HarmonyPatch]
        public static class EnsureExpanded
        {
            public static bool Prepare() => BiomeLordsLink.Method("EnsureExpanded") != null;

            public static MethodBase TargetMethod() => BiomeLordsLink.Method("EnsureExpanded");

            [HarmonyPrefix]
            public static bool Prefix() => !BiomeLordsLink.OwnsInventory;
        }

        [HarmonyPatch]
        public static class FindExtraRowSlot
        {
            public static bool Prepare() => BiomeLordsLink.Method("FindExtraRowSlot") != null;

            public static MethodBase TargetMethod() => BiomeLordsLink.Method("FindExtraRowSlot");

            [HarmonyPrefix]
            public static bool Prefix(ref Vector2i __result)
            {
                if (!BiomeLordsLink.OwnsInventory)
                    return true;
                __result = new Vector2i(-1, -1);
                return false;
            }
        }
    }
}
