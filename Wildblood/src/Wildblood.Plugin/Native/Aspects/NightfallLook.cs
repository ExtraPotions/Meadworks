// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Nightfall's sky drawn into one step of the game's environment. The game has chosen the weather and the time of
    /// day it is about to draw; this blends both toward the storm at midnight by the share <see cref="NightfallBlend"/>
    /// shows. The time swings the short way round the clock to midnight - an evening darkens on through dusk, a
    /// morning goes back through dawn, and when the fight ends the day returns the same way - and the game's four
    /// light weights (day, night, morning, evening) are worked out again for that time exactly as the game does, so
    /// the sun and moon, sky, fog and ambient light all agree. The weather blends through the game's own blend between
    /// two weathers; the storm's rain and its lightning and thunder take over half way, its rain sound at three
    /// quarters, as in the game's own weather changes. At full strength the storm is drawn as itself. Nothing is kept:
    /// the time of day is the game's own again straight after the step (<see cref="Patches.NightfallSkyPatch"/>).
    /// </summary>
    internal static class NightfallLook
    {
        /// <summary>Midnight, as a fraction of the game's day (its noon is 0.5).</summary>
        private const float Midnight = 0f;

        /// <summary>
        /// Blends the weather and the four light weights the game is about to draw toward the storm at midnight, and
        /// returns the time of day, as a fraction of the day, to draw them at.
        /// </summary>
        public static float Apply(EnvMan man, float shown, ref EnvSetup env, ref float day, ref float night,
            ref float morning, ref float evening)
        {
            float fraction = Mathf.Repeat(Mathf.LerpAngle(man.m_smoothDayFraction * 360f, Midnight * 360f, shown), 360f)
                / 360f;
            Weights(man, fraction, out day, out night, out morning, out evening);
            env = Weather(man, env, NightfallBlend.Storm, shown);
            return fraction;
        }

        // The game's own four light weights for a time of day, as its environment step works them out.
        private static void Weights(EnvMan man, float f, out float day, out float night, out float morning,
            out float evening)
        {
            float high = man.m_sunHorizonTransitionH;
            float low = man.m_sunHorizonTransitionL;
            night = Mathf.Pow(Mathf.Max(1f - Mathf.Clamp01(f / 0.25f), Mathf.Clamp01((f - 0.75f) / 0.25f)), 0.5f);
            day = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(f - 0.5f) / 0.25f), 0.5f);
            morning = Mathf.Min(Mathf.Clamp01(1f - (f - 0.26f) / -low), Mathf.Clamp01(1f - (f - 0.26f) / high));
            evening = Mathf.Min(Mathf.Clamp01(1f - (f - 0.74f) / -high), Mathf.Clamp01(1f - (f - 0.74f) / low));
            float scale = 1f / (night + day + morning + evening);
            night *= scale;
            day *= scale;
            morning *= scale;
            evening *= scale;
        }

        // The weather part way to the storm: a fresh blend of the two, so neither the game's weather nor the storm's
        // own setup is ever changed; the storm itself once fully drawn, or the game's weather if the storm is missing.
        private static EnvSetup Weather(EnvMan man, EnvSetup real, EnvSetup? storm, float shown)
        {
            if (storm == null || shown >= 1f)
            {
                return storm ?? real;
            }
            EnvSetup mixed = man.InterpolateEnvironment(real, storm, shown);
            if (shown >= 0.5f)
            {
                mixed.m_psystems = storm.m_psystems; // the rain
                mixed.m_psystemsOutsideOnly = storm.m_psystemsOutsideOnly;
                mixed.m_envObject = storm.m_envObject; // the storm's own objects: its lightning and thunder
                mixed.m_isWet = storm.m_isWet; // wet-looking ground and gear
            }
            return mixed;
        }
    }
}
