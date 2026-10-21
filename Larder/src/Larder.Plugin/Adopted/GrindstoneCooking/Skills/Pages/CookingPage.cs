// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Adapted from ValheimMods at 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Skills/Pages/CookingPage.cs.
// ValheimMods - Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE, 2026: Larder namespace/keys, effective module gates and shared Skills/UI seams;
// native reference interaction, item, food, experience-scope and presentation algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Cooking's page in the info pane: the kitchen perks with the game's own craft-time bonus, the extra food and
    /// saved ingredients, and how experience scales. The numbers come from the helpers the perks themselves use
    /// (<see cref="Perks"/>) at the player's own Cooking level, the level a cook's kitchen uses.
    /// </summary>
    public static class CookingPage
    {
        public static void Write(SkillPage page)
        {
            page.About = LarderText.Text("cooking_about");
            KitchenLines(page);
            YieldLines(page);
            ExperienceLines(page);
        }

        private static void KitchenLines(SkillPage page)
        {
            float level = page.Level;
            page.Heading(LarderText.Text("kitchen"));
            if (KitchenSettings.CookingSpeed.Value > 0f)
                page.Line(LarderText.Format("cooking_speed_value", P(Perks.CookingSpeed(level) - 1f)), LarderText.Text("cooking_speed"), LarderText.Text("cooking_speed_tip"));
            if (KitchenSettings.ExtraBurnTime.Value > 0f)
                page.Line(LarderText.Format("burn_delay_value", P(Perks.ExtraBurnTime(level))), LarderText.Text("burn_delay"), LarderText.Text("burn_delay_tip"));
            CraftTime(page);
            if (KitchenSettings.FermentingSpeed.Value > 0f)
                page.Line(LarderText.Format("fermenting_speed_value", P(Perks.FermentingSpeed(level) - 1f)), LarderText.Text("fermenting_speed"), LarderText.Text("fermenting_speed_tip"));
        }

        private static void CraftTime(SkillPage page)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_craftDurationSkillMaxDecrease <= 0f) return;
            page.Line(LarderText.Format("craft_time_value", P(page.Factor * gui.m_craftDurationSkillMaxDecrease)), LarderText.Text("craft_time"), LarderText.Text("craft_time_tip"));
        }

        private static void YieldLines(SkillPage page)
        {
            float level = page.Level;
            if (KitchenSettings.ExtraFoodChance.Value > 0f)
                page.Line(LarderText.Format("extra_food_value", P(Perks.ExtraFoodChance(level)), Perks.ExtraFoodAmount), LarderText.Text("extra_food"), LarderText.Text("extra_food_tip"));
            if (KitchenSettings.IngredientSaveChance.Value > 0f)
                page.Line(LarderText.Format("ingredient_saved_value", P(Perks.IngredientSaveChance(level))), LarderText.Text("ingredient_saved_title"), LarderText.Text("ingredient_saved_tip"));
        }

        private static void ExperienceLines(SkillPage page)
        {
            float tierMax = Mathf.Max(1f, ExperienceSettings.TierMaximum.Value);
            bool tier = ExperienceSettings.TierScaling.Value && tierMax > 1f;
            float discovery = Mathf.Max(1f, ExperienceSettings.DiscoveryMultiplier.Value);
            float multiplier = XpScaling.Multiplier;
            if (!tier && discovery <= 1f && multiplier == 1f) return;
            page.Heading(LarderText.Text("experience"));
            if (tier) page.Line(LarderText.Format("rich_dishes_value", N(tierMax)), LarderText.Text("rich_dishes"), TierTip(tierMax));
            if (discovery > 1f) page.Line(LarderText.Format("new_dish_value", N(discovery)), LarderText.Text("new_dish"), LarderText.Text("new_dish_tip"));
            if (multiplier != 1f) page.Line(LarderText.Format("kitchen_experience", N(multiplier)));
        }

        private static string TierTip(float tierMax)
        {
            float reference = Mathf.Max(1f, ExperienceSettings.TierReferenceValue.Value);
            return LarderText.Format("rich_dishes_tip", N(reference), N(tierMax), N(reference * tierMax));
        }

        private static string P(float share) => SkillPage.Percent(share);

        private static string N(float value) => SkillPage.Number(value);
    }
}
