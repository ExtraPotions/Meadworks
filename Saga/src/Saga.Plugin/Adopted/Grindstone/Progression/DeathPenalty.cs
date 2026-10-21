// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/DeathPenalty.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Skill loss on death, for every skill. Player.OnDeath calls global::Skills.OnDeath on the dying player's own client,
    /// unless the "no skill drain" buff from a recent death is active or the world resets skills on death (then
    /// global::Skills.Clear runs instead, untouched here). global::Skills.OnDeath calls LowerAllSkills(m_DeathLowerFactor *
    /// Game.m_skillReductionRate), which takes that share off every level and zeroes every skill's progress toward
    /// its next level (m_accumulator). The game's factor is 0.05.
    /// For that one call the factor becomes "Skill Loss On Death" (the world's death penalty modifier still scales
    /// it), and with "Lose Progress On Death" off every skill's progress is put back after LowerAllSkills. With no
    /// loss and progress kept nothing is lowered, and the game's "skills lowered" message is not shown either.
    /// Skill Loss Overrides give listed skills their own percent (see SkillLossOverrideRuntime).
    /// </summary>
    public static class DeathPenalty
    {
        private static bool keepProgress;

        [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.OnDeath))]
        private static class OnDeath
        {
            [HarmonyPrefix]
            private static bool Prefix(global::Skills __instance, out float __state)
            {
                __state = __instance.m_DeathLowerFactor;
                if (!NativeSaga.Active("progression")) return true;
                float loss = Mathf.Clamp(DeathSettings.SkillLoss.Value, 0f, 100f) / 100f;
                keepProgress = !DeathSettings.LoseProgress.Value;
                if (loss <= 0f && keepProgress && !SkillLossOverrideRuntime.AnyLoss)
                    return false;
                __instance.m_DeathLowerFactor = SkillLossOverrideRuntime.Arm(loss);
                return true;
            }

            [HarmonyFinalizer]
            private static void Finalizer(global::Skills __instance, float __state)
            {
                __instance.m_DeathLowerFactor = __state;
                keepProgress = false;
                SkillLossOverrideRuntime.Disarm();
            }
        }

        [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.LowerAllSkills))]
        private static class LowerAll
        {
            [HarmonyPrefix]
            private static void Prefix(global::Skills __instance, out Dictionary<global::Skills.SkillType, float> __state) =>
                __state = keepProgress ? Progress(__instance) : null;

            [HarmonyPostfix]
            private static void Postfix(global::Skills __instance, Dictionary<global::Skills.SkillType, float> __state)
            {
                if (__state == null)
                    return;
                foreach (KeyValuePair<global::Skills.SkillType, float> saved in __state)
                    if (__instance.m_skillData.TryGetValue(saved.Key, out global::Skills.Skill skill))
                        skill.m_accumulator = saved.Value;
            }
        }

        /// <summary>Every skill's progress toward its next level.</summary>
        private static Dictionary<global::Skills.SkillType, float> Progress(global::Skills skills)
        {
            Dictionary<global::Skills.SkillType, float> progress = new Dictionary<global::Skills.SkillType, float>();
            foreach (KeyValuePair<global::Skills.SkillType, global::Skills.Skill> skill in skills.m_skillData)
                progress[skill.Key] = skill.Value.m_accumulator;
            return progress;
        }
    }
}
