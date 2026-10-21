// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/BaseValues.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the replace-or-keep rule for base health and stamina and the skill
// stamina sum became pure functions over the settings and skill factors; keeping the player's own value per player
// stays in the plugin.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Exertion
{
    /// <summary>Base health and stamina, and stamina added by skills (design D4).</summary>
    public static class BaseValues
    {
        public const float DefaultHealth = 25f;
        public const float DefaultStamina = 75f;

        /// <summary>
        /// A base value: the configured one once it differs from its default; at its default the player's own (the
        /// game's or another mod's) stands.
        /// </summary>
        public static float Effective(float own, float configured, float defaultValue)
        {
            return LarderMath.Approximately(configured, defaultValue) ? own : configured;
        }

        /// <summary>The stamina the five skills add: each setting (value at level 100) times that skill's level factor.</summary>
        public static float SkillStamina(ExertionSettings settings, float run, float jump, float sneak, float swim, float fishing)
        {
            return Bonus(settings.RunSkillStamina, run) + Bonus(settings.JumpSkillStamina, jump) + Bonus(settings.SneakSkillStamina, sneak)
                + Bonus(settings.SwimSkillStamina, swim) + Bonus(settings.FishingSkillStamina, fishing);
        }

        private static float Bonus(float atSkill100, float skillFactor) => atSkill100 == 0f ? 0f : atSkill100 * skillFactor;
    }
}
