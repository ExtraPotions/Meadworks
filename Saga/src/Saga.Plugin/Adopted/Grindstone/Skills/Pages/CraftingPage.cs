// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/CraftingPage.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Crafting's page in the info pane, from the game's code: shorter crafts and a chance of one more item at stations
    /// that train Crafting (InventoryGui.UpdateRecipe and DoCrafting, with the live InventoryGui's timings and chances),
    /// and cheaper building with a tool whose pieces train Crafting (Player.GetBuildStamina and GetPlaceDurability):
    /// the tool in hand when it is one, else the first such item in ObjectDB. GrindstoneSkills changes none of it.
    /// </summary>
    public static class CraftingPage
    {
        /// <summary>The game's build stamina saving at level 100.</summary>
        private const float BuildStaminaAt100 = 0.5f;

        public static void Write(SkillPage page)
        {
            ItemDrop.ItemData tool = BuildTool(page.Player);
            page.About = tool != null
                ? SagaText.Text("gs_f09d48b433888a7f")
                : SagaText.Text("gs_eab1d17ab1daf592");
            if (InventoryGui.instance != null)
                Stations(page, InventoryGui.instance);
            if (tool != null)
                Building(page, tool);
        }

        private static void Stations(SkillPage page, InventoryGui gui)
        {
            float time = 1f - page.Factor * gui.m_craftDurationSkillMaxDecrease;
            page.Line(SagaText.Format("gs_960ff0b5396198ef", SkillPage.Percent(1f - time)), SagaText.Text("gs_b32dedba0f445151"),
                SagaText.Format("gs_b9fe995c3ae68100", SkillPage.Duration(gui.m_craftDuration * time), SkillPage.Duration(gui.m_craftDuration), SkillPage.Duration(gui.m_multiCraftDuration * time), gui.m_multiCraftAmount, SkillPage.Percent(gui.m_craftDurationSkillMaxDecrease)));
            page.Line(SagaText.Format("gs_6c8366858c6026ee", SkillPage.Percent(page.Factor * gui.m_craftBonusChance)), SagaText.Text("gs_8730a91ce2c13e0a"),
                SagaText.Format("gs_81ec8ba727cd1549", gui.m_craftBonusAmount, SkillPage.Percent(gui.m_craftBonusChance)));
        }

        /// <summary>The tool's stamina for building, removing and repairing, and its wear when its wear follows Crafting.</summary>
        private static void Building(SkillPage page, ItemDrop.ItemData tool)
        {
            string name = Localized(tool.m_shared.m_name);
            string stamina = SkillPage.Percent(BuildStaminaAt100 * page.Factor);
            if (tool.m_shared.m_placementDurabilitySkill != global::Skills.SkillType.Crafting)
            {
                page.Line(SagaText.Format("gs_7f6706c059bf3f4c", name, stamina), name,
                    SagaText.Format("gs_465fba0680e55f33", SkillPage.Percent(BuildStaminaAt100)));
                return;
            }
            float wear = tool.m_shared.m_placementDurabilityMax;
            page.Line(SagaText.Format("gs_d23ab66c233bf0be", name, stamina, SkillPage.Percent(wear * page.Factor)), name,
                SagaText.Format("gs_0e203ac2cc5b623f", SkillPage.Percent(BuildStaminaAt100), SkillPage.Percent(wear)));
        }

        /// <summary>The build tool whose pieces train Crafting: the one in hand when it is one, else the first in ObjectDB.</summary>
        private static ItemDrop.ItemData BuildTool(Player player)
        {
            ItemDrop.ItemData held = player.GetRightItem();
            if (TrainsCrafting(held))
                return held;
            if (ObjectDB.instance == null)
                return null;
            foreach (GameObject prefab in ObjectDB.instance.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null && TrainsCrafting(drop.m_itemData))
                    return drop.m_itemData;
            }
            return null;
        }

        private static bool TrainsCrafting(ItemDrop.ItemData item) =>
            item != null && item.m_shared.m_buildPieces != null && item.m_shared.m_buildPieces.m_skill == global::Skills.SkillType.Crafting;

        private static string Localized(string token) =>
            GameLocalization.instance != null ? GameLocalization.instance.Localize(token) : token ?? "";
    }
}
