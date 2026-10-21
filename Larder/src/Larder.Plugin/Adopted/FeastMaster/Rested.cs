// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/Rested.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using BepInEx.Configuration;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// The Rested effect: base duration, duration per comfort level above 1, and its regeneration multipliers.
    /// The game's values live on the effect asset, so the entries are bound from the item database with them as
    /// defaults, in section 2. Every rest clones the asset, so the configured values are written into it, and into
    /// the local player's running effect, on load and on every change: the regeneration follows at once, a new
    /// duration the next time the player rests.
    /// </summary>
    public static class Rested
    {
        public static ConfigEntry<float> Duration { get; private set; }
        public static ConfigEntry<float> DurationPerComfort { get; private set; }
        public static ConfigEntry<float> StaminaRegen { get; private set; }
        public static ConfigEntry<float> HealthRegen { get; private set; }
        public static ConfigEntry<float> EitrRegen { get; private set; }

        private static bool written;
        internal static void Reset()
        {
            Duration=null;DurationPerComfort=null;StaminaRegen=null;HealthRegen=null;EitrRegen=null;written=false;
        }

        /// <summary>Binds the entries once the effect asset is known; returns whether they were bound now.</summary>
        public static bool Bind(ObjectDB db)
        {
            if (Duration != null || !(db.GetStatusEffect(SEMan.s_statusEffectRested) is SE_Rested asset))
                return false;
            NativeBindings config = NativeFeastMaster.Bindings;
            string section = Settings.StaminaRegenSection;
            Duration = config.Bind(section, "Rested Duration", asset.m_baseTTL,
                "Seconds the Rested effect lasts at comfort level 1. Takes effect the next time a player rests.");
            DurationPerComfort = config.Bind(section, "Rested Duration Per Comfort", asset.m_TTLPerComfortLevel,
                "Seconds added to Rested Duration for every comfort level above 1. Takes effect the next time a player rests.");
            StaminaRegen = config.Bind(section, "Rested Stamina Regen", asset.m_staminaRegenMultiplier,
                "Stamina regeneration multiplier while rested (1 = no bonus). Combines with the other regen rules like a mead's.");
            HealthRegen = config.Bind(section, "Rested Health Regen", asset.m_healthRegenMultiplier,
                "Health regeneration multiplier while rested (1 = no bonus).");
            EitrRegen = config.Bind(section, "Rested Eitr Regen", asset.m_eitrRegenMultiplier,
                "Eitr regeneration multiplier while rested (1 = no bonus).");
            return true;
        }

        /// <summary>
        /// Writes the configured values into the asset and the local player's running effect, like a food's values:
        /// only once one is changed, and with the defaults (the asset's own) once set back.
        /// </summary>
        public static void ApplyAll()
        {
            if (Duration == null || ObjectDB.instance == null)
                return;
            written |= Customized.Any(Duration, DurationPerComfort, StaminaRegen, HealthRegen, EitrRegen);
            if (!written)
                return;
            Apply(ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectRested) as SE_Rested);
            Player player = Player.m_localPlayer;
            if (player != null)
                Apply(player.GetSEMan().GetStatusEffect(SEMan.s_statusEffectRested) as SE_Rested);
        }

        private static void Apply(SE_Rested rested)
        {
            if (rested == null)
                return;
            rested.m_baseTTL = NativeFeastMaster.Value(Duration);
            rested.m_TTLPerComfortLevel = NativeFeastMaster.Value(DurationPerComfort);
            rested.m_staminaRegenMultiplier = NativeFeastMaster.Value(StaminaRegen);
            rested.m_healthRegenMultiplier = NativeFeastMaster.Value(HealthRegen);
            rested.m_eitrRegenMultiplier = NativeFeastMaster.Value(EitrRegen);
        }
    }
}
