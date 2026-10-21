// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Skills/Pages/ForagingPage.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Foraging's page in the info pane: the extra yield of a pick at the player's level, the experience per pick, and
    /// the discovery and sweep perks. The numbers are the synced settings read as the features read them: the game's
    /// extra-item roll (skill factor times the chance <see cref="ForagePick"/> sets), <see cref="ForageXp"/>'s scale and
    /// <see cref="ForageSweep.Radius"/>.
    /// </summary>
    public static class ForagingPage
    {
        /// <summary>The highest biome step, the Ashlands' and the Deep North's (<see cref="MineXp.BiomeStep"/>).</summary>
        private const int LastBiomeStep = 6;

        public static void Write(SkillPage page)
        {
            page.About = SagaText.Text("gs_ec4c635b9289ba5a");
            if (!ForagingSkill.Active)
            {
                page.Line(SagaText.Text("gs_7696a4652d17002c"));
                return;
            }
            Yield(page);
            Experience(page);
            Perks(page);
        }

        private static void Yield(SkillPage page)
        {
            float atHundred = Mathf.Clamp01(ForagePerkSettings.ExtraYieldAt100.Value / 100f);
            page.Line(SagaText.Format("gs_70f7ba79f1cd1117", SkillPage.Percent(page.Factor * atHundred)), SagaText.Text("gs_8c8244467e1d9443"),
                SagaText.Format("gs_06a7a8b10630f5cb", SkillPage.Percent(atHundred)));
        }

        private static void Experience(SkillPage page)
        {
            float perPick = Mathf.Max(0f, ForagingSettings.ExperiencePerPick.Value);
            if (perPick <= 0f)
                return;
            float step = Mathf.Max(0f, ForagingSettings.BiomeStep.Value) / 100f;
            float most = perPick * (1f + step * LastBiomeStep);
            string range = most > perPick ? SagaText.Format("gs_c1e3e989e9fcf21c", SkillPage.Number(perPick), SkillPage.Number(most)) : SkillPage.Number(perPick);
            page.Line(SagaText.Format("gs_cdf65cf737f07070", range), SagaText.Text("gs_8eab0f09df013b1b"),
                SagaText.Format("gs_a97b8bbdb2026ef2", SkillPage.Number(perPick), SkillPage.Percent(step), SkillPage.Number(most)));
        }

        private static void Perks(SkillPage page)
        {
            float discovery = ForagingSettings.DiscoveryMultiplier.Value;
            if (discovery > 1f)
                page.Perk("Discovery", 0f,
                    SagaText.Format("gs_b9dd9cbeaae17462", SkillPage.Number(discovery)));
            float reach = Mathf.Max(0f, ForagePerkSettings.SweepRadiusAt100.Value);
            float unlock = reach > 0f ? ForagePerkSettings.SweepLevel.Value : 100f + 1f;
            page.Perk(SagaText.Text("gs_310b02ef1be9f84e"), unlock, SweepTip(page.Level, unlock, reach));
        }

        private static string SweepTip(float level, float unlock, float reachAt100)
        {
            string What = SagaText.Text("gs_ffbda2dc0b99a6e7");
            if (level >= unlock)
                return SagaText.Format("gs_6f487da58721754d", What, SkillPage.Number(ForageSweep.Radius(level)), SkillPage.Number(reachAt100));
            return SagaText.Format("gs_4979522cd3fe9ac1", What, SkillPage.Number(ForageSweep.Radius(unlock)), string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", unlock), SkillPage.Number(reachAt100));
        }
    }
}
