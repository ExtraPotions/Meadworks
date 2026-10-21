// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/MagicPages.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The pages of the game's two magic skills in the info pane: the damage a spell rolls and the cut in its costs at the
    /// player's level (<see cref="WeaponLines"/>), then what the player's known staves summon and shield at that level
    /// (<see cref="StaffLines"/>).
    /// </summary>
    public static class MagicPages
    {
        public static void ElementalMagic(SkillPage page)
        {
            page.About = SagaText.Text("gs_c246a3b2e1deeea2");
            WeaponLines.Damage(page, SagaText.Text("gs_c0501d4dfb217156"), "");
            WeaponLines.Cost(page, SagaText.Text("gs_813474e323f15d8a"), SagaText.Text("gs_4c7bd1bafdbd4e24"));
            StaffLines.Write(page);
        }

        public static void BloodMagic(SkillPage page)
        {
            page.About = SagaText.Text("gs_ae3c5cc718835b53");
            WeaponLines.Cost(page, SagaText.Text("gs_cd92d4c6c888064c"), SagaText.Text("gs_f51ef8fe7cdf61ad"));
            if (!StaffLines.Write(page))
                page.Line(SagaText.Text("gs_4d0e8c02e2fe3511"), SagaText.Text("gs_1bd372c9bbecd7dc"),
                    SagaText.Text("gs_5f54a7896102aac8"));
        }
    }
}
