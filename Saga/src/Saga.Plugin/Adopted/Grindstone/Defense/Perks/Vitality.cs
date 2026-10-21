// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Perks/Vitality.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Max Health and Food Health. Player.GetTotalFoodValue starts from the base health (m_baseHP, 25) and adds every
    /// active food's current health; UpdateFood calls it every second on the player's own client and passes the total
    /// to SetMaxHealth, which the health bar, the save and every other reader of max health use. This postfix runs
    /// after the cooking stars' (<see cref="FoodTotals"/>, a lower priority runs later), so Food Health scales what
    /// food gives with its stars and whatever another mod added, then Max Health is added flat. Only the local player
    /// is changed: GetSkillLevel knows nothing of anyone else's skills.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetTotalFoodValue))]
    public static class Vitality
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(Player __instance, ref float hp)
        {
            if (!DefenseSkill.Active || !DefenseSkill.IsLocal(__instance))
                return;
            float level = DefenseSkill.Local();
            float food = Mathf.Max(0f, hp - __instance.m_baseHP);
            hp += food * DefenseSkill.Share(DefenseSettings.FoodHealth.Value, level);
            hp += BonusHealth(level);
        }

        /// <summary>The flat Max Health bonus at a level.</summary>
        public static float BonusHealth(float level) => Mathf.Max(0f, DefenseSettings.MaxHealth.Value) * DefenseSkill.Factor(level);
    }
}
