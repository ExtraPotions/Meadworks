// Kindle boundary for the adopted OpenKeep 3.0.0 salvage settings.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Kindle.Config;
namespace ExtraPotions.Kindle.Runtime.Salvage
{
    internal sealed class SalvageValue<T>
    {
        readonly Func<T> read;
        internal SalvageValue(Func<T> read) { this.read = read; }
        public T Value => read();
    }
    internal static class SalvageSettings
    {
        static SalvageValue<T> Read<T>(KindleSetting<T> setting) => new SalvageValue<T>(() => Plugin.Settings != null ? Plugin.Settings.Get(setting) : setting.Default);
        internal static readonly SalvageValue<bool> Enabled = Read(KindleSettings.Salvage.Enabled);
        internal static readonly SalvageValue<float> ReturnFraction = Read(KindleSettings.Salvage.ReturnFraction);
        internal static readonly SalvageValue<RoundingMode> Rounding = new SalvageValue<RoundingMode>(() => (RoundingMode)(int)(Plugin.Settings != null ? Plugin.Settings.Get(KindleSettings.Salvage.Rounding) : SalvageRounding.Round));
        internal static readonly SalvageValue<bool> AtLeastOne = Read(KindleSettings.Salvage.AtLeastOne);
        internal static readonly SalvageValue<bool> UpgradeMaterials = Read(KindleSettings.Salvage.UpgradeMaterials);
        internal static readonly SalvageValue<bool> RequireKnownRecipe = Read(KindleSettings.Salvage.RequireKnownRecipe);
        internal static readonly SalvageValue<bool> RequireStation = Read(KindleSettings.Salvage.RequireStation);
        internal static readonly SalvageValue<bool> SkipModData = Read(KindleSettings.Salvage.SkipItemsWithModData);
        internal static readonly SalvageValue<string> ModDataPrefixes = Read(KindleSettings.Salvage.ModDataPrefixes);
        internal static readonly SalvageValue<bool> NearbyChests = Read(KindleSettings.Salvage.NearbyChests);
    }
}
