// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Bites/FishingConditions.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The time and weather fish like. Day time and weather are the same on every machine (the environment follows the
    /// world's clock and seed), so the fish's owner and the angler agree without anything being sent.
    /// <list type="bullet">
    /// <item>Dawn and dusk: the game's own curve (Fish uses it for jumping): 1 at a quarter and three quarters of the day,
    /// fading to 0 at noon and midnight. Bites grow by Dawn And Dusk Bite Bonus times it.</item>
    /// <item>Rain (EnvMan.IsWet): bites grow by Rain Bite Bonus.</item>
    /// <item>Night (EnvMan.IsNight): the big-one chance grows by Night Big One Bonus.</item>
    /// </list>
    /// </summary>
    public static class FishingConditions
    {
        /// <summary>0..1: how close the day is to the height of dawn or dusk.</summary>
        public static float DawnDusk()
        {
            EnvMan env = EnvMan.instance;
            if (env == null)
                return 0f;
            float day = env.GetDayFraction();
            return Mathf.Clamp01(1f - Mathf.Abs(Mathf.Abs(day * 2f - 1f) - 0.5f) * 2f);
        }

        public static float BiteFactor()
        {
            float dawnDusk = 1f + FishSkill.Percent(FishingBiteSettings.DawnDuskBonus.Value) * DawnDusk();
            return EnvMan.IsWet() ? dawnDusk * (1f + FishSkill.Percent(FishingBiteSettings.RainBonus.Value)) : dawnDusk;
        }

        public static float BigOneFactor() =>
            EnvMan.IsNight() ? 1f + FishSkill.Percent(FishingBigFishSettings.NightBonus.Value) : 1f;

        /// <summary>"Good fishing: dusk, rain" or "Quiet water", for the angler's species sense.</summary>
        public static string Describe()
        {
            List<string> good = new List<string>();
            if (DawnDusk() >= 0.5f && EnvMan.instance != null)
                good.Add(EnvMan.instance.GetDayFraction() < 0.5f ? SagaText.Text("gs_8326e09ce4e90a41") : SagaText.Text("gs_29aef83195b27118"));
            if (EnvMan.IsWet())
                good.Add(SagaText.Text("gs_319b44c570a417ff"));
            if (EnvMan.IsNight())
                good.Add(SagaText.Text("gs_155b269b44bc63af"));
            return good.Count == 0 ? SagaText.Text("gs_7cd4507032ee4bab") : SagaText.Format("gs_2f6abe987a621882", string.Join(", ", good));
        }
    }
}
