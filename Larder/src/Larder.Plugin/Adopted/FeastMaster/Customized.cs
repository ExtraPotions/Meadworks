// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/Customized.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Whether settings differ from their defaults. A default always means "leave the game alone", so these checks
    /// decide which patches <see cref="PatchSwitch"/> installs and which items <see cref="ItemValues"/> writes.
    /// </summary>
    public static class Customized
    {
        public static bool Any(params ConfigEntryBase[] entries)
        {
            foreach (ConfigEntryBase entry in entries)
            {
                if (entry != null && !Same(NativeFeastMaster.ValueBoxed(entry), entry.DefaultValue))
                    return true;
            }
            return false;
        }

        /// <summary>One float entry, compared without boxing or a params array (spawn hooks and timers call it).</summary>
        public static bool Any(ConfigEntry<float> entry) => entry != null && !Same(NativeFeastMaster.Value(entry), (float)entry.DefaultValue);

        /// <summary>
        /// Floats are compared with a small tolerance: a default read from the game (a food's health, a cook time)
        /// is written to the .cfg with 7 significant digits and may not read back as the exact same float.
        /// </summary>
        private static bool Same(object value, object defaultValue)
        {
            if (value is float a && defaultValue is float b)
                return Same(a, b);
            return Equals(value, defaultValue);
        }

        private static bool Same(float a, float b) => Math.Abs(a - b) <= 1e-5f * Math.Max(1f, Math.Abs(b));

        /// <summary>Any food whose entry <paramref name="key"/> (Vigor, EitrVigor) is changed.</summary>
        public static bool AnyFood(string key)
        {
            foreach (Dictionary<string, ConfigEntry<float>> configs in FeastMasterData.FoodConfigs.Values)
            {
                if (Any(configs[key]))
                    return true;
            }
            return false;
        }

        /// <summary>The values written into a food's item data: its own five, or a global multiplier.</summary>
        public static bool FoodValues(Dictionary<string, ConfigEntry<float>> configs)
        {
            return GlobalFoodValues()
                || Any(configs[FeastMasterData.Health]) || Any(configs[FeastMasterData.Stamina]) || Any(configs[FeastMasterData.Duration])
                || Any(configs[FeastMasterData.HealthRegen]) || Any(configs[FeastMasterData.Eitr]);
        }

        public static bool AnyFoodValues()
        {
            if (GlobalFoodValues())
                return true;
            foreach (Dictionary<string, ConfigEntry<float>> configs in FeastMasterData.FoodConfigs.Values)
            {
                if (FoodValues(configs))
                    return true;
            }
            return false;
        }

        private static bool GlobalFoodValues()
        {
            return Any(FeastMasterData.HealthModifier) || Any(FeastMasterData.StaminaModifier) || Any(FeastMasterData.DurationModifier)
                || Any(FeastMasterData.HealthRegenModifier) || Any(FeastMasterData.EitrModifier);
        }

        public static bool Mead(MeadEffectConfig config)
        {
            return Any(config.Duration, config.HealthOverTime, config.StaminaOverTime, config.EitrOverTime,
                config.HealthRegenMultiplier, config.StaminaRegenMultiplier, config.EitrRegenMultiplier,
                config.RunStaminaModifier, config.JumpStaminaModifier);
        }

        public static bool AnyMead()
        {
            foreach (MeadEffectConfig config in FeastMasterData.MeadConfigs.Values)
            {
                if (Mead(config))
                    return true;
            }
            return false;
        }
    }
}
