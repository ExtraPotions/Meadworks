// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Skills/Pages/SummonLine.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A staff summon's line on a magic skill's page, from its SpawnAbility as the game applies it to each creature it
    /// spawns: damage times 1 + the caster's level of m_copySkill × m_copySkillToRandomFactor (the creature's random skill
    /// factor), and the last m_levelUpSettings step whose level the caster meets sets the creature's level (its stars) and
    /// how many may follow the caster at once (Tameable sends the oldest beyond that away).
    /// </summary>
    internal static class SummonLine
    {
        /// <summary>"Skeleton 1★, damage +30%", the steps in the tip; nothing when the summon does not grow with the level.</summary>
        public static void Write(SkillPage page, string staff, SpawnAbility summon)
        {
            List<SpawnAbility.LevelUpSettings> steps = Steps(summon);
            float damage = DamageBonus(page, summon);
            if (steps.Count == 0 && damage <= 0f)
                return;
            string name = Creature(summon);
            SpawnAbility.LevelUpSettings step = Reached(page, summon);
            int stars = step != null ? Mathf.Max(0, step.m_setLevel - 1) : 0;
            page.Line(Text(name, stars, damage), name, Tip(staff, summon, steps, step));
        }

        private static string Text(string name, int stars, float damage)
        {
            if (stars <= 0 && damage <= 0f)
                return SagaText.Format("gs_dfdcf3e877b7f068", name);
            string starText = stars > 0 ? $" {stars}★" : "";
            return damage > 0f ? SagaText.Format("gs_1b35b20d1da8f43d", name, starText, SkillPage.Percent(damage)) : name + starText;
        }

        /// <summary>The extra damage share at the player's level, as SpawnAbility writes it into the creature's random skill factor.</summary>
        private static float DamageBonus(SkillPage page, SpawnAbility summon)
        {
            if (summon.m_copySkill == global::Skills.SkillType.None || summon.m_copySkillToRandomFactor <= 0f)
                return 0f;
            return ExtraPotions.Meadworks.Crucible.MeadworksSkills.EffectiveLevel(page.Player, summon.m_copySkill) * summon.m_copySkillToRandomFactor;
        }

        /// <summary>The step SpawnAbility applies: the last one listed whose skill level the caster has; null for none.</summary>
        private static SpawnAbility.LevelUpSettings Reached(SkillPage page, SpawnAbility summon)
        {
            if (summon.m_levelUpSettings == null)
                return null;
            for (int i = summon.m_levelUpSettings.Count - 1; i >= 0; i--)
            {
                SpawnAbility.LevelUpSettings step = summon.m_levelUpSettings[i];
                if (step != null && ExtraPotions.Meadworks.Crucible.MeadworksSkills.EffectiveLevel(page.Player, step.m_skill) >= step.m_skillLevel)
                    return step;
            }
            return null;
        }

        /// <summary>The summon's level-up steps, lowest level first.</summary>
        private static List<SpawnAbility.LevelUpSettings> Steps(SpawnAbility summon)
        {
            List<SpawnAbility.LevelUpSettings> steps = new List<SpawnAbility.LevelUpSettings>();
            if (summon.m_levelUpSettings != null)
                steps.AddRange(summon.m_levelUpSettings.FindAll(step => step != null));
            steps.Sort((a, b) => a.m_skillLevel.CompareTo(b.m_skillLevel));
            return steps;
        }

        private static string Tip(string staff, SpawnAbility summon, List<SpawnAbility.LevelUpSettings> steps, SpawnAbility.LevelUpSettings step)
        {
            List<string> parts = new List<string> { SagaText.Format("gs_882c12abf6e0901f", staff) };
            string stars = StarSteps(steps);
            if (stars.Length > 0)
                parts.Add(stars);
            if (summon.m_copySkill != global::Skills.SkillType.None && summon.m_copySkillToRandomFactor > 0f)
                parts.Add(SagaText.Format("gs_563c42fb08c91d9c", SkillPage.Percent(summon.m_copySkillToRandomFactor * 100f)));
            string follow = Followers(summon, step);
            if (follow.Length > 0)
                parts.Add(follow);
            return string.Join(" ", parts);
        }

        /// <summary>"1★ from level 30, 2★ from 60. A star adds its base health again and half its damage." or "".</summary>
        private static string StarSteps(List<SpawnAbility.LevelUpSettings> steps)
        {
            List<string> texts = new List<string>();
            foreach (SpawnAbility.LevelUpSettings step in steps)
            {
                if (step.m_setLevel > 1)
                    texts.Add(SagaText.Format("gs_f331b7ae92779c12", step.m_setLevel - 1, step.m_skillLevel));
            }
            return texts.Count == 0 ? "" : string.Join(", ", texts) + SagaText.Text("gs_4a0af12e56c08771");
        }

        /// <summary>How many may follow the caster at the step reached, or "" when the step sets no limit.</summary>
        private static string Followers(SpawnAbility summon, SpawnAbility.LevelUpSettings step)
        {
            if (step == null)
                return "";
            if (summon.m_setMaxInstancesFromWeaponLevel)
                return SagaText.Text("gs_e92258b6b6515f06");
            return step.m_maxSpawns > 0 ? SagaText.Format("gs_44b1da4a5ff228e0", step.m_maxSpawns) : "";
        }

        /// <summary>The summoned creature's name in the player's language (the first of its prefabs).</summary>
        private static string Creature(SpawnAbility summon)
        {
            GameObject prefab = summon.m_spawnPrefab != null && summon.m_spawnPrefab.Length > 0 ? summon.m_spawnPrefab[0] : null;
            if (prefab == null)
                return SagaText.Text("gs_759c226246309ebb");
            Character creature = prefab.GetComponent<Character>();
            return creature != null ? WeaponLines.Name(creature.m_name) : prefab.name;
        }
    }
}
