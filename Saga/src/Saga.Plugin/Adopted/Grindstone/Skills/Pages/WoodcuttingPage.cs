// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/WoodcuttingPage.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Woodcutting's page in the info pane: the game's own damage on wood at the player's level (global::Skills.GetRandomSkillRange),
    /// then what the Woodcutting module adds to swings, logs and fells, each left out while its setting turns it off. The
    /// numbers are the synced settings scaled by the level exactly as the features scale them (<see cref="WoodSkill.Share"/>,
    /// <see cref="Finds.Chance"/>, <see cref="OldGrowth.SizeShare"/>, <see cref="WoodXp.TierFactor"/>).
    /// </summary>
    public static class WoodcuttingPage
    {
        public static void Write(SkillPage page)
        {
            page.About = SagaText.Text("gs_3b697407f56fea8f");
            GameDamage(page);
            if (!WoodSkill.Active)
            {
                page.Line(SagaText.Text("gs_3ff8e74c9fab51a4"));
                return;
            }
            Chopping(page);
            page.Heading("Felling");
            Felling(page);
        }

        private static void GameDamage(SkillPage page)
        {
            page.Player.GetSkills().GetRandomSkillRange(out float min, out float max, page.Type);
            page.Line(SagaText.Format("gs_588777923c29f4c8", SkillPage.Range(min, max)), SagaText.Text("gs_8b61c02814756136"),
                SagaText.Text("gs_c07e11e714590fe4"));
        }

        private static void Chopping(SkillPage page)
        {
            SwingLine(page);
            CleanSplitLine(page);
            OldGrowthLine(page);
            ExperienceLine(page);
        }

        private static void Felling(SkillPage page)
        {
            TimberLine(page);
            DominoLine(page);
            ChanceLine(page, SagaText.Text("gs_c8991dd4ef0235f8"), FellPerkSettings.CleanFellAt100.Value,
                SagaText.Text("gs_80ab4775881c4e81"));
            ChanceLine(page, SagaText.Text("gs_023402ce0baf70cc"), FellPerkSettings.ReplantingAt100.Value,
                SagaText.Text("gs_c89b5c84480caa3b"));
            FindsLine(page);
        }

        private static void SwingLine(SkillPage page)
        {
            float refund = SwingPerkSettings.StaminaRefund.Value;
            float wear = SwingPerkSettings.WearReduction.Value;
            if (refund <= 0f && wear <= 0f)
                return;
            page.Line(SagaText.Format("gs_4d952e9ae8702e35", Share(refund, page.Level), Share(wear, page.Level)), SagaText.Text("gs_f1a8556a3688c789"),
                SagaText.Format("gs_96a8811c0a314d8f", Share(refund, WoodSkill.MaxLevel), Share(wear, WoodSkill.MaxLevel)));
        }

        private static void CleanSplitLine(SkillPage page)
        {
            float chance = CleanSplitSettings.ChanceAt100.Value;
            if (chance <= 0f)
                return;
            string bonus = SkillPage.Percent(CleanSplitSettings.WoodBonus.Value / 100f);
            page.Line(SagaText.Format("gs_926aca41312e8639", Share(chance, page.Level), bonus), SagaText.Text("gs_362c9187dab0097d"),
                SagaText.Format("gs_c26f47fde05d66ca", bonus, Share(chance, WoodSkill.MaxLevel)));
        }

        private static void OldGrowthLine(SkillPage page)
        {
            float bonus = OldGrowthSettings.Bonus.Value;
            if (bonus <= 0f)
                return;
            float start = OldGrowthSettings.Start.Value;
            float full = Mathf.Max(start, OldGrowthSettings.Full.Value);
            page.Line(SagaText.Format("gs_1b22e899181f8212", BiggestTree(bonus, page.Level)), SagaText.Text("gs_2eeb29400caa18f7"),
                SagaText.Format("gs_185cb3982ab9bda4", SkillPage.Number(start), SkillPage.Number(full), BiggestTree(bonus, WoodSkill.MaxLevel)));
        }

        /// <summary>Old growth's bonus for the largest tree of a kind at this level, as the feature adds it to a log's wood.</summary>
        private static string BiggestTree(float bonus, float level) => SkillPage.Percent(WoodSkill.Share(bonus, level) * OldGrowth.SizeShare(1f));

        private static void ExperienceLine(SkillPage page)
        {
            float fell = Mathf.Max(0f, WoodExperienceSettings.FellExperience.Value) * WoodXp.Multiplier;
            float split = Mathf.Max(0f, WoodExperienceSettings.SplitExperience.Value) * WoodXp.Multiplier;
            if (fell <= 0f && split <= 0f)
                return;
            page.Line(SagaText.Format("gs_f58ce48e18a9365b", SkillPage.Number(fell), SkillPage.Number(split)), SagaText.Text("gs_8eab0f09df013b1b"), ExperienceTip());
        }

        private static string ExperienceTip()
        {
            string tip = SagaText.Format("gs_cf0fe75d45035d34", SkillPage.Number(WoodXp.Multiplier));
            if (WoodExperienceSettings.TierScaling.Value)
                tip += SagaText.Format("gs_f66fe07ea9bb5eac", SkillPage.Number(WoodXp.TierFactor(2)), SkillPage.Number(WoodXp.TierFactor(4)));
            float small = WoodExperienceSettings.SmallWoodHealth.Value;
            if (small > 0f)
                tip += SagaText.Format("gs_dd0b09649283815f", SkillPage.Number(small), SkillPage.Percent(Mathf.Clamp01(WoodExperienceSettings.SmallWoodExperience.Value / 100f)));
            float discovery = Mathf.Max(1f, WoodExperienceSettings.DiscoveryMultiplier.Value);
            if (discovery > 1f)
                tip += SagaText.Format("gs_372060978466b35e", SkillPage.Number(discovery));
            return tip;
        }

        private static void TimberLine(SkillPage page)
        {
            float push = Mathf.Min(TimberSettings.FallPushAt100.Value, TimberSettings.MaxFallPush);
            float safety = Mathf.Min(TimberSettings.LogSafetyAt100.Value, TimberSettings.MaxLogSafety);
            if (push <= 0f && safety <= 0f)
                return;
            float level = page.Level;
            page.Line(SagaText.Format("gs_047d10cc10dafab7", Share(push, level), Share(safety, level)), SagaText.Text("gs_9365cf95e77bfbbe"),
                SagaText.Format("gs_0be189541b987e86", Times(push, level), Times(push, WoodSkill.MaxLevel), Share(safety, WoodSkill.MaxLevel)));
        }

        private static void DominoLine(SkillPage page)
        {
            float impact = DominoSettings.ImpactAt100.Value;
            if (impact <= 0f)
                return;
            page.Line(SagaText.Format("gs_9ef5a58116c4a28e", Share(impact, page.Level)), SagaText.Text("gs_d758a7c46020350f"),
                SagaText.Format("gs_a06171d54441e7e2", Times(impact, page.Level), DominoSettings.MaxChain.Value, Times(impact, WoodSkill.MaxLevel)));
        }

        private static void FindsLine(SkillPage page)
        {
            if (FindSettings.ChanceAt0.Value <= 0f && FindSettings.ChanceAt100.Value <= 0f)
                return;
            page.Line(SagaText.Format("gs_b4990a127ab7b50d", SkillPage.Percent(Finds.Chance(WoodSkill.Factor(page.Level)))), "Finds",
                SagaText.Format("gs_ec3fd2e3f114f019", SkillPage.Percent(Finds.Chance(1f))));
        }

        /// <summary>A chance that grows from nothing at level 0 to its setting at 100; no line while the setting is 0.</summary>
        private static void ChanceLine(SkillPage page, string term, float percentAt100, string tip)
        {
            if (percentAt100 > 0f)
                page.Line($"{term} {Share(percentAt100, page.Level)}", term, SagaText.Format("gs_0532e9efaf819805", tip, Share(percentAt100, WoodSkill.MaxLevel)));
        }

        private static string Share(float percentAt100, float level) => SkillPage.Percent(WoodSkill.Share(percentAt100, level));

        /// <summary>A multiplier of one plus the level's share: 1 + 200% × 0.5 is "2".</summary>
        private static string Times(float percentAt100, float level) => SkillPage.Number(1f + WoodSkill.Share(percentAt100, level));
    }
}
