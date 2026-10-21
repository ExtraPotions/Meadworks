// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/WeaponLines.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The lines the game's weapon skills share on their pages, from the game's own formulas (GrindstoneSkills leaves
    /// these skills as the game has them): the share of a weapon's damage a hit rolls (global::Skills.GetRandomSkillRange: 25–55%
    /// at level 0 up to 85–100% at 100) and the cut in what an attack costs (Attack.GetAttackStamina, GetAttackEitr and
    /// GetAttackHealth, ItemData.GetDrawStaminaDrain: 33% at level 100, linear from 0).
    /// </summary>
    internal static class WeaponLines
    {
        /// <summary>The cut in an attack's stamina, eitr and health cost, and in a bow's draw drain, at level 100.</summary>
        public const float CostCut = 0.33f;

        /// <summary>A melee skill's page: what it is, the damage a hit rolls and the attack stamina.</summary>
        public static void Melee(SkillPage page, string about)
        {
            page.About = about;
            Damage(page, SagaText.Text("gs_ebec2abc7c072c1c"), "");
            Cost(page, SagaText.Text("gs_c5954f5501e76b7a"), SagaText.Text("gs_d4683b5a66997d6f"));
        }

        /// <summary>"Damage 47–77% of the weapon's": the range a hit rolls in at this level, the range at 100 in the tip.</summary>
        public static void Damage(SkillPage page, string whose, string more)
        {
            Roll(page, out float low, out float high);
            page.Line(SagaText.Format("gs_1c371dd5d4ef834d", SkillPage.Range(low, high), whose), SagaText.Text("gs_0c874f581a4534f1"),
                SagaText.Format("gs_aed7907894145102", more));
        }

        /// <summary>"Attack stamina -12.2%": a cost the skill cuts by up to <see cref="CostCut"/>, the thing itself explained on hover.</summary>
        public static void Cost(SkillPage page, string what, string more)
        {
            page.Line($"{what} -{SkillPage.Percent(CostCut * page.Factor)}", what, SagaText.Format("gs_19e68013252b4338", SkillPage.Percent(CostCut), more));
        }

        /// <summary>The share of a weapon's damage a hit rolls at the player's level, lowest and highest: the game's own range.</summary>
        public static void Roll(SkillPage page, out float low, out float high) =>
            page.Player.GetSkills().GetRandomSkillRange(out low, out high, page.Type);

        /// <summary>The weapon in the player's hands when it trains the page's skill; otherwise null.</summary>
        public static ItemDrop.ItemData Held(SkillPage page)
        {
            ItemDrop.ItemData weapon = page.Player.GetCurrentWeapon();
            return weapon != null && weapon.m_shared.m_skillType == page.Type ? weapon : null;
        }

        /// <summary>A game token ("$item_bow") in the player's language.</summary>
        public static string Name(string token) =>
            GameLocalization.instance != null ? GameLocalization.instance.Localize(token) : token;

        /// <summary>A short time with up to two decimals: "0.64 s".</summary>
        public static string Seconds(float seconds) => SagaText.Format("gs_dae58fb2bf4be815", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##}", seconds));
    }
}
