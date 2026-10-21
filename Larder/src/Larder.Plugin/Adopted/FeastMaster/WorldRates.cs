// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/WorldRates.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using BepInEx.Configuration;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// World rate overrides. The world modifiers Game.m_foodRate, m_staminaRate, m_moveStaminaRate and
    /// m_staminaRegenRate are static fields set from the world's global keys in Game.UpdateWorldRates; an override
    /// above 0 replaces the value after every such update. A setting change re-runs the game's own update through
    /// ZoneSystem.UpdateWorldRates (which reads the current keys), so switching an override back to 0 restores
    /// the world's value.
    /// </summary>
    public static class WorldRates
    {


        public static void HookConfig(ConfigFile config)
        {
            config.SettingChanged += Changed;
            config.ConfigReloaded += Reloaded;
        }
        public static void UnhookConfig(ConfigFile config)
        {
            config.SettingChanged -= Changed;
            config.ConfigReloaded -= Reloaded;
        }
        static void Changed(object sender, SettingChangedEventArgs args)
        {
            if (args.ChangedSetting.Definition.Section == Settings.WorldRatesSection) Refresh();
        }
        static void Reloaded(object sender, System.EventArgs args) => Refresh();

        /// <summary>Runs the game's world rate update (the postfix then applies the overrides).</summary>
        public static void Refresh()
        {
            if (ZoneSystem.instance != null && Game.instance != null)
                ZoneSystem.instance.UpdateWorldRates();
        }

        public static void ApplyOverrides()
        {
            Override(ref Game.m_foodRate, NativeFeastMaster.Value(Settings.FoodRate));
            Override(ref Game.m_staminaRate, NativeFeastMaster.Value(Settings.StaminaRate));
            Override(ref Game.m_moveStaminaRate, NativeFeastMaster.Value(Settings.MoveStaminaRate));
            Override(ref Game.m_staminaRegenRate, NativeFeastMaster.Value(Settings.StaminaRegenRate));
        }

        private static void Override(ref float rate, float value)
        {
            if (value > 0f)
                rate = value;
        }
    }

    /// <summary>Installed while an override is set; installing or removing it recomputes the rates at once.</summary>
    [HarmonyPatch(typeof(Game), nameof(Game.UpdateWorldRates))]
    public static class WorldRatesPatch
    {
        public static bool Prepare() => Customized.Any(Settings.FoodRate, Settings.StaminaRate, Settings.MoveStaminaRate, Settings.StaminaRegenRate);

        public static void Installed() => WorldRates.Refresh();

        public static void Removed() => WorldRates.Refresh();

        [HarmonyPostfix]
        public static void Postfix() => WorldRates.ApplyOverrides();
    }
}
