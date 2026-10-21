using HarmonyLib;
using Burrow.Core;

namespace Burrow.Reach
{
    /// <summary>
    /// The Pull modifier held while Craft is pressed: the missing materials of the selected recipe move from
    /// storage into the inventory first, so the craft pays from the inventory alone, for as many crafts as the next
    /// press makes (the Batch stepper's amount, or the game's Shift + Craft without it). When they do not all fit
    /// (or storage lacks them) nothing is crafted and the centre message says so; what was moved stays.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnCraftPressed))]
    public static class CraftPullPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(InventoryGui __instance)
        {
            Recipe recipe = __instance.m_selectedRecipe.Recipe;
            Player player = Player.m_localPlayer;
            if (recipe == null || player == null || !Keys.Held(ReachSettings.PullModifier))
                return true;
            ItemDrop.ItemData upgrade = __instance.m_selectedRecipe.ItemData;
            int quality = upgrade == null ? 1 : upgrade.m_quality + 1;
            if (!ReachRules.Active(ReachRules.FromQuality(quality)))
                return true;
            int crafts = upgrade == null ? ((ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyLStick") || __instance.m_touchMultiCrafting) ? __instance.m_multiCraftAmount : 1) : 1;
            if (PullAll(player, recipe, quality, crafts))
                return true;
            Messages.Center("$burrow_nofit");
            return false;
        }

        /// <summary>Every requirement's shortfall, or for a single ingredient recipe the first one storage can complete.</summary>
        private static bool PullAll(Player player, Recipe recipe, int quality, int multiplier)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            bool complete = true;
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (Requirements.Skipped(station, requirement))
                    continue;
                bool covered = PullOne(player.GetInventory(), requirement, requirement.GetAmount(quality) * multiplier);
                if (recipe.m_requireOnlyOneIngredient && covered)
                    return true;
                complete &= covered;
            }
            return complete && !recipe.m_requireOnlyOneIngredient;
        }

        private static bool PullOne(Inventory inventory, Piece.Requirement requirement, int need)
        {
            string name = Requirements.Name(requirement);
            int shortfall = need - inventory.CountItems(name);
            if (shortfall <= 0)
                return true;
            return ReachPull.Move(inventory, item => ReachCount.Matches(item, name, -1, true), shortfall) >= shortfall;
        }
    }
}
