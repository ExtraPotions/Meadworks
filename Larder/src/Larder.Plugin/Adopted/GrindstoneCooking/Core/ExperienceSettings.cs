// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Core/ExperienceSettings.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Section 5: Cooking experience on top of the game's own amounts. With Tier Scaling off, Discovery Multiplier 1
    /// and Experience Multiplier 1, a player earns exactly what the game gives (plus the fermenter credit).
    /// Synced.
    /// </summary>
    public static class ExperienceSettings
    {
        public const string Section = "7 - Cooking";

        public static CookingSetting<float> Multiplier { get; private set; }
        public static CookingSetting<bool> TierScaling { get; private set; }
        public static CookingSetting<float> TierReferenceValue { get; private set; }
        public static CookingSetting<float> TierMaximum { get; private set; }
        public static CookingSetting<float> DiscoveryMultiplier { get; private set; }
        public static CookingSetting<float> FermenterTap { get; private set; }

        public static void Initialize(ConfigFile config)
        {
            Multiplier = CookingSettings.Bind(config, Section, "Experience Multiplier", 1f,
                "Multiplies all Cooking experience earned at kitchens.", acceptableValues: Settings.UpTo(10f));
            TierScaling = CookingSettings.Bind(config, Section, "Tier Scaling", true,
                "Richer dishes teach more: experience is multiplied by the dish's health + stamina + eitr divided by Tier Reference Food Value, never below 1.");
            TierReferenceValue = CookingSettings.Bind(config, Section, "Tier Reference Food Value", 50f,
                "The food value (health + stamina + eitr) that earns the game's own experience.", acceptableValues: new AcceptableValueRange<float>(1f, 500f), scope: ConfigScope.ServerAuthoritative, tags: new object[] { new ConfigDependency(() => TierScaling.Value, () => "Requires Cooking: Tier Scaling.") });
            TierMaximum = CookingSettings.Bind(config, Section, "Tier Maximum Multiplier", 3f,
                "The most Tier Scaling can multiply experience by.", acceptableValues: new AcceptableValueRange<float>(1f, 10f), scope: ConfigScope.ServerAuthoritative, tags: new object[] { new ConfigDependency(() => TierScaling.Value, () => "Requires Cooking: Tier Scaling.") });
            DiscoveryMultiplier = CookingSettings.Bind(config, Section, "Discovery Multiplier", 3f,
                "Experience multiplier the first time a character makes each dish.", acceptableValues: new AcceptableValueRange<float>(1f, 10f));
            FermenterTap = CookingSettings.Bind(config, Section, "Fermenter Tap Experience", 1f,
                "Cooking experience for tapping a fermenter (the game gives none). 0 turns it off.", acceptableValues: Settings.UpTo(10f));
        }
    }
}
