// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/BlockingPage.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Blocking's page in the info pane, from the game's code (Humanoid.BlockAttack, ItemDrop.ItemData.GetBlockPower):
    /// block armor +50% at level 100, what that makes of the blocker in hand and of its parry, and a parry effect that
    /// grows with the Blocking level (SE_Shield) when the blocker or the player has one. GrindstoneSkills changes none
    /// of it; its own Defense skill has its own page.
    /// </summary>
    public static class BlockingPage
    {
        /// <summary>The game's block armor bonus at level 100 and its parry window, in seconds.</summary>
        private const float ArmorAt100 = 0.5f;
        private const float ParryWindow = 0.25f;

        public static void Write(SkillPage page)
        {
            page.About = SagaText.Text("gs_4b7aafabb3f674b6");
            page.Line(SagaText.Format("gs_f24f35f2f9779cba", SkillPage.Percent(ArmorAt100 * page.Factor)), SagaText.Text("gs_290c2e8dbd9f76a9"),
                SagaText.Format("gs_779064ee3169fcce", SkillPage.Percent(ArmorAt100)));
            ItemDrop.ItemData blocker = page.Player.GetCurrentBlocker();
            if (blocker != null)
                Held(page, blocker);
        }

        /// <summary>The blocker in hand (the left-hand item, else the weapon): its block armor now and its parry.</summary>
        private static void Held(SkillPage page, ItemDrop.ItemData blocker)
        {
            float armor = blocker.GetBlockPower(page.Factor);
            float untrained = blocker.GetBaseBlockPower();
            string name = Localized(blocker.m_shared.m_name);
            page.Line(SagaText.Format("gs_34f2ad61a3133e64", name, string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", armor)), name,
                SagaText.Format("gs_ebfeb018642c7594", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", untrained), string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", untrained * (1f + ArmorAt100))));
            float bonus = blocker.m_shared.m_timedBlockBonus;
            if (bonus <= 1f)
                return;
            page.Line(SagaText.Format("gs_760004b85e3da7a4", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", armor * bonus)), "Parry",
                SagaText.Format("gs_6360f661002bf81b", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00}", ParryWindow), SkillPage.Number(bonus)));
            ParryEffect(page, blocker);
        }

        /// <summary>
        /// The effect a parry gives, chosen as the game chooses it (the blocker's, else the player's), when it is a shield
        /// that absorbs more damage per Blocking level: how much it absorbs at the player's level and world level.
        /// </summary>
        private static void ParryEffect(SkillPage page, ItemDrop.ItemData blocker)
        {
            StatusEffect effect = blocker.m_shared.m_perfectBlockStatusEffect != null ? blocker.m_shared.m_perfectBlockStatusEffect : page.Player.m_perfectBlockStatusEffect;
            if (!(effect is SE_Shield shield) || shield.m_absorbDamagePerSkillLevel <= 0f)
                return;
            float absorb = shield.m_absorbDamage + shield.m_absorbDamagePerSkillLevel * page.Level;
            if (Game.m_worldLevel > 0)
                absorb += shield.m_absorbDamageWorldLevel * Game.m_worldLevel;
            string name = Localized(shield.m_name);
            if (name.Length == 0)
                name = SagaText.Text("gs_7d7fc67da4498e76");
            page.Line(SagaText.Format("gs_9df1fb4a0d45d0c9", name, string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}", absorb)), name,
                SagaText.Format("gs_ca2285fa8e9a6a1b", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##}", shield.m_absorbDamagePerSkillLevel)));
        }

        private static string Localized(string token) =>
            GameLocalization.instance != null ? GameLocalization.instance.Localize(token) : token ?? "";
    }
}
