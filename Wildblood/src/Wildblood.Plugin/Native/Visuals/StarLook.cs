// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Visuals
{
    /// <summary>
    /// Gives a starred creature the game's own look for its star count - the tint, and the extra horns or plates some
    /// creatures grow - which the game picks from the creature's level. Every elite stays at vanilla level 1 (see
    /// DECISIONS.md), so without this a two-star Neck would keep the plain one-level green. One star takes the game's
    /// one-star look; two or more take the highest look the prefab has, since the game ships none past two stars. Size
    /// stays the rule file's: the game's per-level scale is put back straight after. Runs on every machine from the star
    /// count in the ZDO, so every player sees the same creature.
    /// </summary>
    public static class StarLook
    {
        /// <summary>Every machine, once the creature's traits are loaded.</summary>
        public static void Apply(Character character, int stars)
        {
            foreach (LevelEffects effects in character.GetComponentsInChildren<LevelEffects>())
            {
                effects.m_character = character; // its own Start may not have run yet, and the tint needs it
                Show(effects, stars);
            }
        }

        /// <summary>The look for <paramref name="stars"/> on one set of level effects, keeping their size.</summary>
        public static void Show(LevelEffects effects, int stars)
        {
            int level = LevelFor(effects, stars);
            if (level <= 1)
            {
                return;
            }
            Vector3 scale = effects.transform.localScale;
            effects.SetupLevelVisualization(level);
            effects.transform.localScale = scale;
        }

        /// <summary>The look the creature these effects belong to wears, or null for the plain one.</summary>
        public static LevelEffects.LevelSetup? SetupFor(LevelEffects effects)
        {
            int level = LevelFor(effects, StarsOf(effects.m_character));
            return level > 1 ? effects.m_levelSetups[level - 2] : null;
        }

        /// <summary>A resolved creature's star count; 0 for anything the mod has not rolled.</summary>
        public static int StarsOf(Character? character)
        {
            EliteController? controller = character != null ? character.GetComponent<EliteController>() : null;
            return controller != null && controller.Ready ? controller.Traits.Stars : 0;
        }

        // The game level whose look fits: one above the stars, capped at the last look the prefab has.
        private static int LevelFor(LevelEffects effects, int stars) =>
            stars <= 0 ? 1 : Mathf.Min(stars + 1, effects.m_levelSetups.Count + 1);
    }
}
