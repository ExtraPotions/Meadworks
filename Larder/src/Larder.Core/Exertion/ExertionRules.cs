// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/GameplayRules.cs (SkillGainPatch, DrowningDamagePatch).
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the skill gain and drowning prefixes became pure functions over a
// Larder skill enum and plain flags.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Exertion
{
    /// <summary>The skills whose experience gain Exertion scales (design D5); Other is every other skill.</summary>
    public enum ExertionSkill
    {
        Run,
        Jump,
        Sneak,
        Swim,
        Fishing,
        Other,
    }

    /// <summary>Drowning damage (design D3) and skill gain multipliers (design D5).</summary>
    public static class ExertionRules
    {
        /// <summary>A drowning hit on a player times Drowning Damage; any other hit unchanged.</summary>
        public static float DrowningDamage(float damage, float multiplier, bool isPlayer, bool isDrowning)
        {
            return isPlayer && isDrowning ? damage * LarderMath.NonNegative(multiplier) : damage;
        }

        /// <summary>The experience factor passed to the game's skill raise, times the skill's gain multiplier.</summary>
        public static float SkillGain(ExertionSkill skill, float factor, ExertionSettings settings)
        {
            return factor * LarderMath.NonNegative(Multiplier(skill, settings));
        }

        private static float Multiplier(ExertionSkill skill, ExertionSettings settings)
        {
            switch (skill)
            {
                case ExertionSkill.Run: return settings.RunSkillGain;
                case ExertionSkill.Jump: return settings.JumpSkillGain;
                case ExertionSkill.Sneak: return settings.SneakSkillGain;
                case ExertionSkill.Swim: return settings.SwimSkillGain;
                case ExertionSkill.Fishing: return settings.FishingSkillGain;
                default: return 1f;
            }
        }
    }
}
