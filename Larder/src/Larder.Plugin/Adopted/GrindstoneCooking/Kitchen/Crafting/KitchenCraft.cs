// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Crafting/KitchenCraft.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Crafting at a kitchen crafting station (cauldron, mead cauldron, prep table). InventoryGui.DoCrafting runs on
    /// the crafting player's client: it rolls the game's bonus food (m_craftBonusChance times the skill factor, for
    /// stackable items), adds the whole amount, multi-craft and bonus included, pays the ingredients, then raises the
    /// skill. Upgrades take their own branch and are left alone.
    /// The prefix swaps in GrindstoneSkills' extra food chance (<see cref="ExtraFood"/>), notes the crafter and the
    /// batches (1, or the multi-craft amount) and starts recording the ingredients (<see cref="CraftRecord"/>). The
    /// postfix may give an ingredient back (<see cref="IngredientSave"/>). The finalizer puts everything back, also when
    /// the craft failed or threw. All of it is local to the crafter.
    /// </summary>
    public static class KitchenCraft
    {
        private static Player crafter;
        private static int batches;
        private static ExtraFood.State extraFood;

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        private static class Crafting
        {
            [HarmonyPrefix]
            private static void Prefix(InventoryGui __instance, Player player) => Begin(__instance, player);

            [HarmonyPostfix]
            private static void Postfix() => Finish();

            [HarmonyFinalizer]
            private static void Finalizer() => End();
        }

        private static void Begin(InventoryGui gui, Player player)
        {
            End();
            CraftingStation station = Player.m_localPlayer == null ? null : Player.m_localPlayer.GetCurrentCraftingStation();
            Recipe recipe = gui.m_craftRecipe;
            if (player == null || !Kitchen.IsKitchen(station) || recipe == null || recipe.m_item == null || gui.m_craftUpgradeItem != null)
                return;
            crafter = player;
            batches = gui.m_multiCrafting ? gui.m_multiCraftAmount : 1;
            extraFood = ExtraFood.Apply();
            CraftRecord.Start();
        }

        private static void Finish()
        {
            if (crafter == null)
                return;
            CraftRecord.Stop();
            IngredientSave.Roll(crafter, batches, CraftRecord.Lots);
        }

        public static void ClearScope() => End();

        private static void End()
        {
            ExtraFood.Restore(extraFood);
            extraFood = default;
            CraftRecord.Clear();
            crafter = null;
            batches = 0;
        }
    }
}
