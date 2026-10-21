using HarmonyLib;

namespace Burrow.Reach
{
    /// <summary>
    /// Counting for recipes: <c>Player.HaveRequirementItems</c> is the last check of <c>HaveRequirements(Recipe...)</c>
    /// (station level, DLC and knowledge come before it), so a false result here means only the items were short.
    /// The postfix re-runs the game's loop with inventory plus reachable containers, quality by quality as the
    /// game does, including the single ingredient rule.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirementItems))]
    public static class RecipeRequirementPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, Recipe piece, bool discover, int qualityLevel, int amount, ref bool __result)
        {
            if (__result || discover || piece == null || !ReachRules.Active(ReachRules.FromQuality(qualityLevel)))
                return;
            __result = HaveWithStorage(__instance, piece, qualityLevel, amount);
        }

        private static bool HaveWithStorage(Player player, Recipe recipe, int qualityLevel, int amount)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (Requirements.Skipped(station, requirement))
                    continue;
                int need = requirement.GetAmount(qualityLevel) * amount;
                int best = BestQualityCount(player.GetInventory(), requirement.m_resItem.m_itemData.m_shared);
                if (recipe.m_requireOnlyOneIngredient)
                {
                    if (best >= need)
                        return true;
                }
                else if (best < need)
                {
                    return false;
                }
            }
            return !recipe.m_requireOnlyOneIngredient;
        }

        /// <summary>The game counts each quality on its own and takes the largest; storage is added per quality.</summary>
        private static int BestQualityCount(Inventory inventory, ItemDrop.ItemData.SharedData shared)
        {
            int best = 0;
            for (int quality = 1; quality <= shared.m_maxQuality; quality++)
            {
                int count = inventory.CountItems(shared.m_name, quality) + ReachCount.InContainers(shared.m_name, quality, true);
                if (count > best)
                    best = count;
            }
            return best;
        }
    }
}
