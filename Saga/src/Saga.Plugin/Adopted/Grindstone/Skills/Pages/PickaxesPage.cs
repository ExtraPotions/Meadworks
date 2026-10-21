// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/PickaxesPage.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Pickaxes' page in the info pane: the game's own damage and swing stamina at the player's level
    /// (global::Skills.GetRandomSkillRange, Attack.GetAttackStamina), then what the Pickaxes module adds (seams and clean strikes,
    /// splash, extra ore, wear, finds, experience) and its three milestones, each left out while its setting turns it off.
    /// The numbers are the synced settings scaled by the level exactly as the features scale them
    /// (<see cref="PickSkill.Share"/>, <see cref="PickSkill.Between"/>, <see cref="CleanStrikes.Multiplier"/>,
    /// <see cref="Splash.Amount"/>, <see cref="MineFinds.Chance"/>, <see cref="MineXp.BiomeStep"/>).
    /// </summary>
    public static class PickaxesPage
    {
        /// <summary>The game's cut in attack stamina at level 100: Attack.GetAttackStamina takes 33% times the skill factor off.</summary>
        private const float StaminaCut = 0.33f;

        public static void Write(SkillPage page)
        {
            page.About = SagaText.Text("gs_4d0e6b254dbc6043");
            GameLines(page);
            if (!PickSkill.Active)
            {
                page.Line(SagaText.Text("gs_3ff8e74c9fab51a4"));
                return;
            }
            SeamLines(page);
            SplashLine(page);
            PerkLines(page);
            FindsLine(page);
            ExperienceLine(page);
            Milestones(page);
        }

        /// <summary>Seams open at all: "0 with the next setting at 0 turns seams off".</summary>
        private static bool SeamsOn => SeamSettings.ChanceAt0.Value > 0f || SeamSettings.ChanceAt100.Value > 0f;

        private static void GameLines(SkillPage page)
        {
            page.Player.GetSkills().GetRandomSkillRange(out float min, out float max, page.Type);
            page.Line(SagaText.Format("gs_dab34ba3166ce6db", SkillPage.Range(min, max)), SagaText.Text("gs_1823586bdb0264c6"),
                SagaText.Text("gs_c7f5b03b0d33de0a"));
            page.Line(SagaText.Format("gs_168db996088ff66d", SkillPage.Percent(StaminaCut * page.Factor)), SagaText.Text("gs_72de669453417606"),
                SagaText.Format("gs_58fdc8a2a1618230", SkillPage.Percent(StaminaCut)));
        }

        private static void SeamLines(SkillPage page)
        {
            if (!SeamsOn)
                return;
            float level = page.Level;
            float chance = PickSkill.Between(SeamSettings.ChanceAt0.Value, SeamSettings.ChanceAt100.Value, level) / 100f;
            float window = Mathf.Max(0.1f, PickSkill.Between(SeamSettings.WindowAt0.Value, SeamSettings.WindowAt100.Value, level));
            page.Line(SagaText.Format("gs_bfa7fe5533af51e7", SkillPage.Percent(chance), SkillPage.Duration(window)), "Seams",
                SagaText.Text("gs_d4f81d22fad0afb5"));
            page.Line(SagaText.Format("gs_83363b1f49bb179b", SkillPage.Number(CleanStrikes.Multiplier(1, level))), SagaText.Text("gs_8c67147bfdabf42e"), CleanStrikeTip());
        }

        private static string CleanStrikeTip()
        {
            float experience = Mathf.Max(0f, PickaxeExperienceSettings.CleanStrike.Value) * MineXp.Multiplier;
            string earns = experience > 0f ? SagaText.Format("gs_20a8f5df95bd38b7", SkillPage.Number(experience)) : "";
            return SagaText.Format("gs_6e0a5753f7b5fbaf", earns);
        }

        private static void SplashLine(SkillPage page)
        {
            float at100 = Mathf.Max(0f, PickaxePerkSettings.SplashDamage.Value);
            if (at100 <= 0f)
                return;
            float amount = Splash.Amount(new Miner(0L, page.Level, ZDOID.None));
            page.Line(SagaText.Format("gs_affec18594959c36", SkillPage.Number(amount)), "Splash",
                SagaText.Format("gs_0f7c04f654571600", SkillPage.Number(at100)));
        }

        private static void PerkLines(SkillPage page)
        {
            float level = page.Level;
            float ore = PickaxePerkSettings.ExtraOreChance.Value;
            if (ore > 0f)
                page.Line(SagaText.Format("gs_a8b39a508438e6c2", Share(ore, level)), SagaText.Text("gs_a61beba911f32cf2"),
                    SagaText.Format("gs_1cb21dd1f40b07ca", Share(ore, PickSkill.MaxLevel)));
            float wear = PickaxePerkSettings.WearReduction.Value;
            if (wear > 0f)
                page.Line(SagaText.Format("gs_4ff2f75081935ddf", Share(wear, level)), SagaText.Text("gs_41901b34eb179b46"),
                    SagaText.Format("gs_a3335dba5149e92e", Share(wear, PickSkill.MaxLevel)));
        }

        private static void FindsLine(SkillPage page)
        {
            if (MineFindSettings.ChanceAt0.Value <= 0f && MineFindSettings.ChanceAt100.Value <= 0f)
                return;
            page.Line(SagaText.Format("gs_7878ec4ba1af9a43", SkillPage.Percent(MineFinds.Chance(page.Level))), "Finds",
                SagaText.Format("gs_a86f7b0423482d24", SkillPage.Percent(MineFinds.Chance(PickSkill.MaxLevel))));
        }

        private static void ExperienceLine(SkillPage page)
        {
            float step = Mathf.Max(0f, PickaxeExperienceSettings.BiomeStep.Value);
            float ore = 1f + Mathf.Max(0f, PickaxeExperienceSettings.OreBonus.Value) / 100f;
            page.Line(SagaText.Format("gs_35463925c1a45163", SkillPage.Number(step), SkillPage.Number(ore)), SagaText.Text("gs_8eab0f09df013b1b"), ExperienceTip(step));
        }

        private static string ExperienceTip(float step)
        {
            string forest = SkillPage.Number(1f + step / 100f * MineXp.BiomeStep(Heightmap.Biome.BlackForest));
            string ashlands = SkillPage.Number(1f + step / 100f * MineXp.BiomeStep(Heightmap.Biome.AshLands));
            string tip = SagaText.Format("gs_d34ac1e8152d5cba", forest, ashlands);
            float discovery = Mathf.Max(0f, PickaxeExperienceSettings.Discovery.Value);
            if (discovery > 0f)
                tip += SagaText.Format("gs_c9a13d5efe76ac5f", SkillPage.Number(discovery));
            if (MineXp.Multiplier != 1f)
                tip += SagaText.Format("gs_516b2b762141c5e4", SkillPage.Number(MineXp.Multiplier));
            return tip;
        }

        private static void Milestones(SkillPage page)
        {
            page.Perk(SagaText.Text("gs_cec15060bfc24d03"), VeinSettings.ReadTheRockLevel.Value, ReadTheRockTip());
            page.Perk(SagaText.Text("gs_8335e4c92c40556a"), VeinSettings.EchoLevel.Value,
                SagaText.Format("gs_53461503ac758d24", SkillPage.Number(VeinSettings.EchoRadius.Value), SkillPage.Duration(VeinSettings.EchoCooldown.Value)));
            if (SeamsOn)
                page.Perk(SagaText.Text("gs_2c60f6170e5032a4"), SeamSettings.UnbrokenLevel.Value, UnbrokenTip());
        }

        private static string ReadTheRockTip()
        {
            string stars = SagaText.Format("gs_3bc36fad4a4c9ac3", Setting(VeinSettings.Chance1.Value), Setting(VeinSettings.Chance2.Value), Setting(VeinSettings.Chance3.Value));
            return SagaText.Format("gs_c851ef6f09992dfa", stars, Setting(VeinSettings.BonusPerStar.Value));
        }

        private static string UnbrokenTip()
        {
            float level = SeamSettings.UnbrokenLevel.Value;
            float first = CleanStrikes.Multiplier(1, level);
            float second = CleanStrikes.Multiplier(2, level);
            float top = CleanStrikes.Multiplier(Mathf.Max(0, SeamSettings.UnbrokenMaxLinks.Value) + 1, level);
            return SagaText.Format("gs_7aa9502af2ef96f3", SkillPage.Number(second), SkillPage.Number(second - first), SkillPage.Number(top));
        }

        /// <summary>A perk's share at this level as a percent, held to 0..1 as the perks hold it.</summary>
        private static string Share(float percentAt100, float level) => SkillPage.Percent(Mathf.Clamp01(PickSkill.Share(percentAt100, level)));

        /// <summary>A percent setting as it reads: 25 is "25%".</summary>
        private static string Setting(float percent) => SkillPage.Percent(Mathf.Max(0f, percent) / 100f);
    }
}
