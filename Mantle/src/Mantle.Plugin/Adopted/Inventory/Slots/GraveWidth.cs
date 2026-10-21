using HarmonyLib;

namespace Mantle.Slots
{
    /// <summary>
    /// A grave keeps the width of the inventory it was made from, but only in the dying player's game: the game makes
    /// the grave's inventory at the tombstone prefab's width whenever the grave loads again (another player's game, the
    /// area loading back in, a relog), and its load refuses every item beyond that width, so the extra columns of a
    /// wide inventory would vanish. While a grave loads its inventory is made wide enough for anything, then set to the
    /// widest column in it. Rows need nothing: the game grows a container's height to its items (<c>UpdateRows</c>).
    /// Runs whether or not this player's module is on, because the grave may be another player's.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.Load))]
    public static class GraveWidth
    {
        private const int LoadWidth = 32;

        [HarmonyPrefix]
        public static void Prefix(Container __instance, out bool __state)
        {
            __state = __instance.m_inventory != null && __instance.GetComponent<TombStone>() != null;
            if (__state)
                __instance.m_inventory.m_width = LoadWidth;
        }

        [HarmonyPostfix]
        public static void Postfix(Container __instance, bool __state)
        {
            if (!__state)
                return;
            int width = __instance.m_width;
            foreach (ItemDrop.ItemData item in __instance.m_inventory.GetAllItems())
                width = System.Math.Max(width, item.m_gridPos.x + 1);
            __instance.m_inventory.m_width = width;
        }
    }
}
