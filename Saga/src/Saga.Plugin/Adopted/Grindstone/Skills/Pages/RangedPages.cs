// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/RangedPages.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The pages of the game's ranged skills in the info pane: the damage a shot rolls (<see cref="WeaponLines"/>), a
    /// bow's draw time and draw stamina (Humanoid.GetAttackDrawPercentage, ItemData.GetDrawStaminaDrain) and a
    /// crossbow's reload time (ItemData.GetWeaponLoadingTime) at the player's level, with the held weapon's own seconds.
    /// </summary>
    public static class RangedPages
    {
        /// <summary>The cut in the time to a full draw at level 100: the game draws in a fifth of the bow's time.</summary>
        private const float DrawCut = 0.8f;

        /// <summary>The cut in a crossbow's reload time at level 100: the game reloads in half the time.</summary>
        private const float ReloadCut = 0.5f;

        public static void Bows(SkillPage page)
        {
            page.About = SagaText.Text("gs_4f2f1bd8e94d405f");
            WeaponLines.Damage(page, SagaText.Text("gs_13159d1dde183653"), SagaText.Text("gs_8b810be9fe1111c0"));
            string bow = HeldTime(page, a => a.m_bowDraw ? a.m_drawDurationMin : 0f, DrawCut);
            page.Line(SagaText.Format("gs_159eea87398f2bcd", SkillPage.Percent(DrawCut * page.Factor)), SagaText.Text("gs_cebfea3ecafbc49d"),
                SagaText.Format("gs_3131da5dd98139c1", bow));
            WeaponLines.Cost(page, SagaText.Text("gs_c4264d6e095a1435"),
                SagaText.Text("gs_ec2e2d0ae10b1951"));
        }

        public static void Crossbows(SkillPage page)
        {
            page.About = SagaText.Text("gs_a84e2346e8006dec");
            WeaponLines.Damage(page, SagaText.Text("gs_3e44acab7e4d30e4"), "");
            string crossbow = HeldTime(page, a => a.m_requiresReload ? a.m_reloadTime : 0f, ReloadCut);
            page.Line(SagaText.Format("gs_72980ab7ca00325c", SkillPage.Percent(ReloadCut * page.Factor)), SagaText.Text("gs_e15490d067be21b3"),
                SagaText.Format("gs_422db27aa8c9e615", crossbow));
        }

        /// <summary>
        /// " Your Arbalest: 2.9 s (from 4 s).": the held weapon's time at the player's level, the game's lerp from its own
        /// time down by <paramref name="cut"/> at level 100; empty with no such weapon in hand.
        /// </summary>
        private static string HeldTime(SkillPage page, System.Func<Attack, float> baseTime, float cut)
        {
            ItemDrop.ItemData weapon = WeaponLines.Held(page);
            float full = weapon == null ? 0f : baseTime(weapon.m_shared.m_attack);
            if (full <= 0f)
                return "";
            float now = Mathf.Lerp(full, full * (1f - cut), page.Factor);
            return SagaText.Format("gs_917ffb3f22b7b853", WeaponLines.Name(weapon.m_shared.m_name), WeaponLines.Seconds(now), WeaponLines.Seconds(full));
        }
    }
}
