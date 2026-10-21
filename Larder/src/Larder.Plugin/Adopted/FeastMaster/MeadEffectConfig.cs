// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/MeadEffectConfig.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using BepInEx.Configuration;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>The nine entries of a mead: the four over-time values and the five bonus modifiers of its status effect.</summary>
    public class MeadEffectConfig
    {
        public ConfigEntry<float> Duration { get; set; }
        public ConfigEntry<float> HealthOverTime { get; set; }
        public ConfigEntry<float> StaminaOverTime { get; set; }
        public ConfigEntry<float> EitrOverTime { get; set; }
        public ConfigEntry<float> HealthRegenMultiplier { get; set; }
        public ConfigEntry<float> StaminaRegenMultiplier { get; set; }
        public ConfigEntry<float> EitrRegenMultiplier { get; set; }
        public ConfigEntry<float> RunStaminaModifier { get; set; }
        public ConfigEntry<float> JumpStaminaModifier { get; set; }
    }
}
