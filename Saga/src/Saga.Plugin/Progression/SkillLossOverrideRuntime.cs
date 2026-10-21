// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-skill Skill Loss Overrides on top of the adapted GrindstoneSkills death penalty.
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Progression;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime
{
    /// <summary>
    /// The death penalty's OnDeath prefix arms this for the one LowerAllSkills call it makes. While armed, the game's
    /// death factor is 1, so LowerAllSkills receives the world's skill reduction rate alone. The prefix below records that
    /// rate and every level, hands the game Skill Loss On Death times the rate (the unchanged behavior for unlisted
    /// skills), and the postfix lowers each listed skill from its recorded level by its own percent instead. With no
    /// override above zero nothing is armed and the adapted penalty runs exactly as before.
    /// </summary>
    public static class SkillLossOverrideRuntime
    {
        private static IReadOnlyDictionary<string, float>? armed;
        private static float globalPercent;
        private static float worldRate;

        private static IReadOnlyDictionary<string, float> Configured() =>
            SkillLossOverrides.Parse(Plugin.Settings?.Get(SagaSettings.Progression.SkillLossOverrides));

        /// <summary>Some listed skill loses levels on death, so a zero global loss must not skip the game's call.</summary>
        public static bool AnyLoss => Configured().Values.Any(v => v > 0f);

        /// <summary>The death factor for this death: <paramref name="loss"/> unchanged without overrides, else 1 (armed).</summary>
        public static float Arm(float loss)
        {
            var overrides = Configured();
            armed = overrides.Count == 0 ? null : overrides;
            globalPercent = loss * 100f;
            return armed == null ? loss : 1f;
        }

        public static void Disarm() => armed = null;

        private static IEnumerable<string> Names(global::Skills.SkillType type)
        {
            yield return type.ToString();
            var definition = MeadworksSkills.Registry.All.FirstOrDefault(d => d.Id.Value == (int)type);
            if (definition == null) yield break;
            yield return definition.Name;
            yield return definition.Identity;
        }

        [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.LowerAllSkills))]
        private static class LowerAll
        {
            [HarmonyPriority(Priority.First)]
            private static void Prefix(global::Skills __instance, ref float factor, out Dictionary<global::Skills.SkillType, float>? __state)
            {
                __state = null;
                if (armed == null) return;
                worldRate = Mathf.Max(0f, factor);
                factor = globalPercent / 100f * worldRate;
                __state = __instance.m_skillData.ToDictionary(p => p.Key, p => p.Value.m_level);
            }

            private static void Postfix(global::Skills __instance, Dictionary<global::Skills.SkillType, float>? __state)
            {
                var overrides = armed;
                if (__state == null || overrides == null) return;
                foreach (var saved in __state)
                {
                    var names = Names(saved.Key).ToList();
                    if (!SkillLossOverrides.Lists(overrides, names)) continue;
                    if (!__instance.m_skillData.TryGetValue(saved.Key, out var skill)) continue;
                    skill.m_level = SkillLossOverrides.LevelAfterDeath(saved.Value, SkillLossOverrides.LossPercent(overrides, names, globalPercent), worldRate);
                }
            }
        }
    }
}
