// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/DefensePage.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Defense's page in the info pane: every bonus at the player's level, the guard perks, and the four milestones with
    /// the level each needs. The numbers are the synced settings scaled by the level exactly as the features scale them
    /// (<see cref="DefenseSkill.Share"/>, <see cref="Vitality.BonusHealth"/>).
    /// </summary>
    public static class DefensePage
    {
        public static void Write(SkillPage page)
        {
            page.About = SagaText.Text("gs_532891f4794ac1b5");
            if (!DefenseSkill.Active)
            {
                page.Line(SagaText.Text("gs_7696a4652d17002c"));
                return;
            }
            Core(page);
            Guard(page);
            Milestones(page);
        }

        private static void Core(SkillPage page)
        {
            float level = page.Level;
            page.Line(SagaText.Format("gs_29f0910901d044ac", SkillPage.Number(Vitality.BonusHealth(level))));
            page.Line(SagaText.Format("gs_056da9d191e8d2bd", Share(DefenseSettings.FoodHealth.Value, level)));
            page.Line(SagaText.Format("gs_28efc6c89e6667dc", Share(DefenseSettings.DamageReduction.Value, level)));
            page.Line(SagaText.Format("gs_5f3d566861ee0078", Share(DefenseSettings.Regeneration.Value, level), SkillPage.Duration(DefenseSettings.RegenerationInterval.Value)),
                SagaText.Text("gs_501cb5ee5de287d5"), SagaText.Format("gs_53bf21c1e054f147", SkillPage.Duration(DefenseSettings.OutOfCombatDelay.Value)));
            page.Line(SagaText.Format("gs_b7b38d5e8c9efa3f", Share(DefenseSettings.Poise.Value, level)), SagaText.Text("gs_87deb420627715ca"),
                SagaText.Text("gs_7afe960e1eaa2fb3"));
            page.Line(SagaText.Format("gs_b2f71c6dc5ee7369", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00}", DefenseSettings.ParryWindow.Value * DefenseSkill.Factor(level))), SagaText.Text("gs_1699300bdf4cdde8"),
                SagaText.Text("gs_ad9ffe834df9578d"));
            page.Line(SagaText.Format("gs_44003a3db195cb25", Share(DefenseSettings.BlockStamina.Value, level), Share(DefenseSettings.DodgeStamina.Value, level)));
        }

        private static void Guard(SkillPage page)
        {
            float level = page.Level;
            page.Heading(SagaText.Text("gs_c8d3fc567bbcbf38"));
            page.Line(SagaText.Format("gs_22a3187db876bfd2", Share(DefenseGuardSettings.ReflexChance.Value, level)), "Reflex",
                SagaText.Text("gs_04645101233851f8"));
            page.Line(SagaText.Format("gs_6470b994ed32abde", Share(DefenseGuardSettings.BashChance.Value, level)), SagaText.Text("gs_1758fa5672125302"),
                SagaText.Text("gs_273e07d69b2879de"));
            page.Line(SagaText.Format("gs_16cf0fb528973268", Share(DefenseGuardSettings.Thorns.Value, level)), SagaText.Text("gs_d20c5a45f3025088"),
                SagaText.Text("gs_d86ac322f854f864"));
            page.Line(SagaText.Format("gs_8ad3aadfed883f8f", Share(DefenseGuardSettings.Adrenaline.Value, level)), SagaText.Text("gs_2608729251008c96"),
                SagaText.Text("gs_8b7b7e245c075c19"));
            page.Line(SagaText.Format("gs_6ff78abae17eb322", Share(DefenseGuardSettings.ShieldWear.Value, level), Share(DefenseGuardSettings.Knockback.Value, level)));
            if (DefenseGuardSettings.DesperationHealth.Value > 0f)
                page.Line(SagaText.Format("gs_7d1d261e8ea112d3", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", DefenseGuardSettings.DesperationHealth.Value), SkillPage.Number(DefenseGuardSettings.DesperationMultiplier.Value)));
        }

        private static void Milestones(SkillPage page)
        {
            page.Perk(SagaText.Text("gs_6e1bdc294002f23d"), DefenseMilestoneSettings.RiposteLevel.Value,
                SagaText.Format("gs_ee1105430f4391d8", SkillPage.Duration(DefenseMilestoneSettings.RiposteWindow.Value), string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", DefenseMilestoneSettings.RiposteDamage.Value)));
            page.Perk(SagaText.Text("gs_8d61ed61f25318cf"), DefenseMilestoneSettings.ShieldWallLevel.Value,
                SagaText.Format("gs_4e0a9a6bc2c286c4", SkillPage.Number(DefenseMilestoneSettings.ShieldWallRadius.Value), string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", DefenseMilestoneSettings.ShieldWallReduction.Value)));
            page.Perk(SagaText.Text("gs_c590bc54b8fa963d"), DefenseMilestoneSettings.HardenedLevel.Value, HardenedTip());
            page.Perk(SagaText.Text("gs_cfe83e821b0faa83"), DefenseMilestoneSettings.LastStandLevel.Value,
                SagaText.Format("gs_b279f167b32add4e", SkillPage.Duration(DefenseMilestoneSettings.LastStandInvulnerability.Value), SkillPage.Duration(DefenseMilestoneSettings.LastStandCooldown.Value)));
        }

        private static string HardenedTip()
        {
            float perStack = DefenseMilestoneSettings.HardenedPerStack.Value;
            int stacks = DefenseMilestoneSettings.HardenedMaxStacks.Value;
            return SagaText.Format("gs_23d2591248a391ea", SkillPage.Number(perStack), stacks, SkillPage.Number(perStack * stacks), SkillPage.Duration(DefenseMilestoneSettings.HardenedDuration.Value));
        }

        private static string Share(float percentAt100, float level) => SkillPage.Percent(DefenseSkill.Share(percentAt100, level));
    }
}
