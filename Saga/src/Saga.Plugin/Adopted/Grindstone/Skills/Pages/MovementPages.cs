// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/MovementPages.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The pages of the game's movement skills in the info pane: Run, Swim, Jump, Sneak, Dodge and Ride, each with what
    /// the game's code does with the level (Player.CheckRun and GetRunSpeedFactor, OnSwimming, Character.Jump,
    /// Player.OnSneaking and UpdateStealth, GetDodgeStaminaUse, Sadle.UpdateRiding and Character.GetRunSpeedFactor).
    /// Stamina costs come from the player's own prefab fields and the world's movement stamina rate where the game
    /// applies it, before gear and status effects. GrindstoneSkills changes none of these skills.
    /// </summary>
    public static class MovementPages
    {
        /// <summary>The game's scales at level 100.</summary>
        private const float RunStaminaAt100 = 0.5f;
        private const float RunSpeedAt100 = 0.25f;
        private const float JumpAt100 = 0.4f;
        private const float SneakStaminaAt100 = 0.75f;
        private const float DodgeStaminaAt100 = 0.5f;
        private const float MountSpeedAt100 = 0.25f;
        private const float MountStaminaAt100 = 0.5f;

        /// <summary>How far enemies see a crouching player, as a share of their range: untrained and at 100, dark and lit.</summary>
        private const float SeenDark = 0.5f;
        private const float SeenDarkAt100 = 0.2f;
        private const float SeenLit = 1f;
        private const float SeenLitAt100 = 0.6f;

        public static void Run(SkillPage page)
        {
            Player player = page.Player;
            float drain = player.m_runStaminaDrain * Game.m_moveStaminaRate;
            float speed = player.m_runSpeed;
            page.About = SagaText.Text("gs_b64c23a0db2e2f7c");
            page.Line(SagaText.Format("gs_942e13fee8aab0a0", SkillPage.Percent(RunStaminaAt100 * page.Factor)), SagaText.Text("gs_5d65fe33073e68bd"),
                SagaText.Format("gs_b8c4799802c40181", Rate(drain * (1f - RunStaminaAt100 * page.Factor)), Rate(drain), SkillPage.Percent(RunStaminaAt100)));
            page.Line(SagaText.Format("gs_d083a1351cd08748", SkillPage.Percent(RunSpeedAt100 * page.Factor)), SagaText.Text("gs_0ceeb90caf7b6fe7"),
                SagaText.Format("gs_aa387342630471d9", SkillPage.Number(speed * (1f + RunSpeedAt100 * page.Factor)), SkillPage.Number(speed), SkillPage.Percent(RunSpeedAt100)));
        }

        public static void Swim(SkillPage page)
        {
            Player player = page.Player;
            float untrained = player.m_swimStaminaDrainMinSkill * Game.m_moveStaminaRate;
            float master = player.m_swimStaminaDrainMaxSkill * Game.m_moveStaminaRate;
            float now = Mathf.Lerp(untrained, master, page.Factor);
            float saving = untrained > 0f ? 1f - now / untrained : 0f;
            page.About = SagaText.Text("gs_0e2ecd6cff22e2eb");
            page.Line(SagaText.Format("gs_6881c5970a9fb9d5", SkillPage.Percent(saving)), SagaText.Text("gs_528cf80bb77e5dc3"),
                SagaText.Format("gs_c4c44cb981470ae7", Rate(now), Rate(untrained), Rate(master)));
        }

        public static void Jump(SkillPage page)
        {
            page.About = SagaText.Text("gs_98e8d80d1a6431a0");
            page.Line(SagaText.Format("gs_cbf569152515cdd3", SkillPage.Percent(JumpAt100 * page.Factor)), SagaText.Text("gs_f5af49b3a67d3352"),
                SagaText.Format("gs_8633ec264f52b1e0", SkillPage.Percent(JumpAt100)));
        }

        public static void Sneak(SkillPage page)
        {
            Player player = page.Player;
            float share = 1f - SneakStaminaAt100 * Mathf.Sqrt(page.Factor);
            page.About = SagaText.Text("gs_77f1d17d0c6e38b9");
            page.Line(SagaText.Format("gs_54adcc831d7136cf", SkillPage.Percent(1f - share)), SagaText.Text("gs_b4caa5d57e09ca92"),
                SagaText.Format("gs_9fc3e7b855a89262", Rate(player.m_sneakStaminaDrain * share), Rate(player.m_sneakStaminaDrain), SkillPage.Percent(SneakStaminaAt100)));
            float dark = Mathf.Lerp(SeenDark, SeenDarkAt100, page.Factor);
            float lit = Mathf.Lerp(SeenLit, SeenLitAt100, page.Factor);
            page.Line(SagaText.Format("gs_5f52b706fc8cfaf0", SkillPage.Percent(dark), SkillPage.Percent(lit)), SagaText.Text("gs_ec40e78f847ec64d"),
                SagaText.Format("gs_e2a363fc0e77966f", SkillPage.Percent(SeenDarkAt100), SkillPage.Percent(SeenLitAt100)));
        }

        public static void Dodge(SkillPage page)
        {
            Player player = page.Player;
            float cost = player.m_dodgeStaminaUsage * (1f - DodgeStaminaAt100 * page.Factor);
            page.About = SagaText.Text("gs_4b88c43f39fbe0b2");
            page.Line(SagaText.Format("gs_c893febd0d9daee2", SkillPage.Percent(DodgeStaminaAt100 * page.Factor)), SagaText.Text("gs_6a3dbb89c98646a2"),
                SagaText.Format("gs_6d96bac421fa9f76", SkillPage.Number(cost), SkillPage.Number(player.m_dodgeStaminaUsage), SkillPage.Percent(DodgeStaminaAt100)));
            float refund = player.m_perfectDodgeStaminaReturnMultiplier;
            if (refund > 0f)
                page.Line(SagaText.Format("gs_be698219fd920a45", SkillPage.Number(cost * refund)), SagaText.Text("gs_746a35fbfd6fdc91"),
                    SagaText.Format("gs_5d5803534023f264", SkillPage.Percent(refund), SkillPage.Number(player.m_perfectDodgeAdrenaline)));
        }

        public static void Ride(SkillPage page)
        {
            page.About = SagaText.Text("gs_c5151abfc8794167");
            page.Line(SagaText.Format("gs_548fd28e99bf2d78", SkillPage.Percent(MountSpeedAt100 * page.Factor)), SagaText.Text("gs_5e1a43119182ce2a"),
                SagaText.Format("gs_495736c26bb86ebc", SkillPage.Percent(MountSpeedAt100)));
            page.Line(SagaText.Format("gs_b649be0012b25985", SkillPage.Percent(MountStaminaAt100 * page.Factor)), SagaText.Text("gs_3050a7affed5a8d1"),
                SagaText.Format("gs_e17e63f0a6175661", SkillPage.Percent(MountStaminaAt100)));
        }

        private static string Rate(float perSecond) => SagaText.Format("gs_79b615b353fb790a", SkillPage.Number(perSecond));
    }
}
