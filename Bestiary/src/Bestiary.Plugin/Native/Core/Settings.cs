// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Coalesced native settings dispatch adapts EliteCreaturesPack 0.7.2 Core/Settings.cs.
// Portions Copyright (C) 2026 Gerald Glasgow at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: one owned C01 event subscription, no Charter/config default writes; the Bone Ballista
// section of EliteCreaturesPack 0.9.0 (ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6).
using System;
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
namespace ExtraPotions.Bestiary.Runtime.Native
{
    internal static class Settings
    {
        private static ConfigFile? file;
        private static bool pending;
        public static event Action? Changed;
        internal static void Initialize(ConfigFile config)
        {
            var values = new NativeConfiguration();
            Mimic.MimicSettings.Initialize(values);
            Slinger.SlingerSettings.Initialize(values);
            RimeGiant.RimeGiantSettings.Initialize(values);
            Kraken.KrakenSettings.Initialize(values);
            Crossbow.XbowSettings.Initialize(values);
            Crossbow.XbowItemSettings.Initialize(values);
            Arsenal.ArsenalSettings.Initialize(values);
            Headsman.HeadsmanSettings.Initialize(values);
            Headsman.GreataxeSettings.Initialize(values);
            Ballista.BallistaSettings.Initialize(values);
            file = config; config.SettingChanged += OnChanged;
        }
        private static void OnChanged(object sender, SettingChangedEventArgs args) => pending = true;
        internal static void Request() => pending = true;
        internal static void Tick() { if (!pending) return; pending = false; Core.SafeCall.Run("native settings", () => Changed?.Invoke()); }
        internal static void Shutdown() { if (file != null) file.SettingChanged -= OnChanged; file = null; Changed = null; pending = false; }
        public static AcceptableValueRange<float> Range(float min, float max) => new AcceptableValueRange<float>(min, max);
        public static void Renew<T>(GatedEntry<T> entry, T oldDefault) { } // Bestiary defaults were bound before adoption; no source cfg migration.
    }
}
