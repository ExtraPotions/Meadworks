// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Experience/FishXp.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Fishing experience, all on the angler's own client (skills live there).
    /// <list type="bullet">
    /// <item><b>Reeling.</b> The game raises Fishing once per second of reeling an empty line and twice per second with a
    /// fish on it (FishingFloat.FixedUpdate); <see cref="FloatScope"/> scales each raise by <see cref="ReelScale"/>:
    /// Empty Reel Experience or Fight Experience, times the Experience Multiplier. Snagged lines count as empty.</item>
    /// <item><b>Catches.</b> <see cref="OnCatch"/>: Catch Experience x the species (<see cref="FishInfo.SpeciesScale"/>) x
    /// 1 + Size Experience Bonus per level above 1.</item>
    /// <item><b>Discoveries.</b> <see cref="OnDiscovery"/>, from the angler's log (<see cref="LogBook"/>): Discovery
    /// Experience for a new species, New Size Experience for a new level of a known one, times the species.</item>
    /// </list>
    /// Credits go through <see cref="FloatScope.RaiseUnscoped"/>, so they are never scaled as reeling; they still go through
    /// Player.RaiseSkill (Rested) and the world's skill-gain rate. The game gives Fishing 0.25 of a point per raise.
    /// </summary>
    public static class FishXp
    {
        public static float Multiplier => Mathf.Max(0f, FishingExperienceSettings.Multiplier.Value);

        /// <summary>The factor for the game's reeling raise, with or without a fish on the line.</summary>
        public static float ReelScale(bool fishOnLine) =>
            FishSkill.Percent(fishOnLine ? FishingExperienceSettings.FightReel.Value : FishingExperienceSettings.EmptyReel.Value) * Multiplier;

        public static void OnCatch(CatchInfo info)
        {
            float size = 1f + FishSkill.Percent(FishingExperienceSettings.SizeBonus.Value) * (info.Level - 1);
            float amount = Mathf.Max(0f, FishingExperienceSettings.Catch.Value) * info.SpeciesScale * size * Multiplier;
            FloatScope.RaiseUnscoped(info.Player, amount);
        }

        /// <summary>The credit for a new species (<paramref name="species"/>) or a new level of a known one.</summary>
        public static void OnDiscovery(CatchInfo info, bool species)
        {
            float setting = species ? FishingExperienceSettings.Discovery.Value : FishingExperienceSettings.NewSize.Value;
            FloatScope.RaiseUnscoped(info.Player, Mathf.Max(0f, setting) * info.SpeciesScale * Multiplier);
        }
    }
}
