using HarmonyLib;

namespace Burrow.Reach
{
    /// <summary>
    /// Single ingredient recipes (<c>m_requireOnlyOneIngredient</c>) pick their material through
    /// <c>Player.GetFirstRequiredItem</c>. When the inventory alone has none but inventory plus storage covers
    /// a requirement, the postfix returns the inventory's stack of it or, failing that, the container's stack;
    /// the game only reads its name and quality, and the payment hook pays the shortfall from storage.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetFirstRequiredItem))]
    public static class FirstRequiredItemPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, Inventory inventory, Recipe recipe, int qualityLevel, ref int amount, ref int extraAmount, int craftMultiplier, ref ItemDrop.ItemData __result)
        {
            if (__result != null || recipe == null || inventory == null || !ReachRules.Active(ReachRules.FromQuality(qualityLevel)))
                return;
            CraftingStation station = __instance.GetCurrentCraftingStation();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (Requirements.Skipped(station, requirement))
                    continue;
                int need = requirement.GetAmount(qualityLevel) * craftMultiplier;
                ItemDrop.ItemData found = Find(inventory, requirement.m_resItem.m_itemData.m_shared, need);
                if (found == null)
                    continue;
                amount = need;
                extraAmount = requirement.m_extraAmountOnlyOneIngredient;
                __result = found;
                return;
            }
        }

        private static ItemDrop.ItemData Find(Inventory inventory, ItemDrop.ItemData.SharedData shared, int need)
        {
            for (int quality = 0; quality <= shared.m_maxQuality; quality++)
            {
                if (inventory.CountItems(shared.m_name, quality) + ReachCount.InContainers(shared.m_name, quality, true) < need)
                    continue;
                ItemDrop.ItemData own = inventory.GetItem(shared.m_name, quality);
                return own ?? ReachCount.FirstInContainers(shared.m_name, quality, true);
            }
            return null;
        }
    }
}
