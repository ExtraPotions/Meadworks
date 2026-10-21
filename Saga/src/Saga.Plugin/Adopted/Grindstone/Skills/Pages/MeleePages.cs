// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/MeleePages.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The pages of the game's melee skills in the info pane: the damage a hit rolls and the attack stamina at the
    /// player's level (<see cref="WeaponLines"/>), plus what one skill does differently: axes on trees, bare fists.
    /// </summary>
    public static class MeleePages
    {
        public static void Swords(SkillPage page) =>
            WeaponLines.Melee(page, SagaText.Text("gs_98cd5136792543c3"));

        public static void Knives(SkillPage page) =>
            WeaponLines.Melee(page, SagaText.Text("gs_59ad1e3d0a38e3ca"));

        public static void Clubs(SkillPage page) =>
            WeaponLines.Melee(page, SagaText.Text("gs_12e59267dc362a60"));

        public static void Polearms(SkillPage page) =>
            WeaponLines.Melee(page, SagaText.Text("gs_c426038bc13061ec"));

        public static void Spears(SkillPage page) =>
            WeaponLines.Melee(page, SagaText.Text("gs_78eefaad618bc983"));

        public static void Axes(SkillPage page)
        {
            WeaponLines.Melee(page, SagaText.Text("gs_4e7473fdafe212a3"));
            page.Line(SagaText.Text("gs_c63fe50d8c4a2043"), "Woodcutting",
                SagaText.Text("gs_65e3465f3e4abfef"));
        }

        public static void Unarmed(SkillPage page)
        {
            WeaponLines.Melee(page, SagaText.Text("gs_ca0959c68d5fd4cb"));
            Fists(page);
        }

        /// <summary>"Bare fists hit for 2–4": the player's fists' damage times the range a hit rolls in.</summary>
        private static void Fists(SkillPage page)
        {
            ItemDrop fists = page.Player.m_unarmedWeapon;
            if (fists == null)
                return;
            float damage = fists.m_itemData.GetDamage().GetTotalDamage();
            if (damage <= 0f)
                return;
            WeaponLines.Roll(page, out float low, out float high);
            page.Line(SagaText.Format("gs_00f2a6196568d16b", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", damage * low), string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", damage * high)), SagaText.Text("gs_d7bd736857dd14e9"),
                SagaText.Text("gs_43757991b744e7b2"));
        }
    }
}
