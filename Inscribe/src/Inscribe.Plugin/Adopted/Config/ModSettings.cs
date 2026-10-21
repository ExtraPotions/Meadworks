// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Crucible boundary for the adopted EliteCrafting gameplay engine (0.7.0, with the 0.10.0 runes, sockets, Rune Table and salvage runes). Reads the existing Inscribe settings at use time.
using System;
using ExtraPotions.Inscribe.Config;
using ExtraPotions.Inscribe.Runtime;
using ExtraPotions.Inscribe.Integration;
namespace ExtraPotions.Inscribe.Adopted.Config
{
    public enum ConfirmMode { HoldShift, Dialog, Off }
    public enum TooltipDetail { Compact, Standard, Full }
    public sealed class LiveSetting<T>
    {
        private readonly Func<T> read;
        public LiveSetting(Func<T> read) { this.read = read; }
        public T Value => read();
        public event EventHandler? SettingChanged;
        internal void Notify() => SettingChanged?.Invoke(this, EventArgs.Empty);
    }
    public static class ModSettings
    {
        static T Read<T>(InscribeSetting<T> key) => Runtime.Plugin.Settings!.Get(key);
        static LiveSetting<T> Bind<T>(InscribeSetting<T> key) => new LiveSetting<T>(() => Read(key));
        static bool Rarity => InscribeModules.Active(InscribeModule.Rarity);
        static bool Display => InscribeModules.Active(InscribeModule.Display);
        public static readonly LiveSetting<bool> AffixEffects = new LiveSetting<bool>(() => Rarity && Read(InscribeSettings.General.InscriptionEffects));
        public static readonly LiveSetting<bool> ModifyEquippedItems = Bind(InscribeSettings.Runes.ModifyEquippedItems);
        public static readonly LiveSetting<bool> SalvageRunes = new LiveSetting<bool>(() => Rarity && Read(InscribeSettings.Runes.RunesFromSalvage));
        public static readonly LiveSetting<bool> RuneTable = new LiveSetting<bool>(() => Rarity && Read(InscribeSettings.Runes.RuneTable));
        public static readonly LiveSetting<bool> BiomeEssences = Bind(InscribeSettings.Runes.BiomeEssences);
        public static readonly LiveSetting<bool> GemsAndSockets = new LiveSetting<bool>(() => Rarity && Read(InscribeSettings.Runes.GemsAndSockets));
        public static readonly LiveSetting<bool> StoneDrops = new LiveSetting<bool>(() => Rarity && Read(InscribeSettings.Drops.RuneDrops));
        public static readonly LiveSetting<bool> MagicItemDrops = new LiveSetting<bool>(() => Rarity && Read(InscribeSettings.Drops.MagicItemDrops));
        public static readonly LiveSetting<bool> ReadOnlyCommandsForEveryone = Bind(InscribeSettings.Commands.ReadOnlyForEveryone);
        public static readonly LiveSetting<ConfirmMode> ConfirmDestructiveStones = new LiveSetting<ConfirmMode>(() => (ConfirmMode)(int)Read(InscribeSettings.Runes.ConfirmDestructiveRunes));
        public static readonly LiveSetting<bool> ColoredItemNames = new LiveSetting<bool>(() => Display && Read(InscribeSettings.Display.ColoredItemNames));
        public static readonly LiveSetting<TooltipDetail> TooltipDetailLevel = new LiveSetting<TooltipDetail>(() => (TooltipDetail)(int)Read(InscribeSettings.Display.TooltipDetail));
        public static readonly LiveSetting<bool> ShowDormantAffixes = Bind(InscribeSettings.Display.ShowDormant);
        public static readonly LiveSetting<bool> GroundGlow = new LiveSetting<bool>(() => Display && Read(InscribeSettings.Glow.GroundGlow));
        public static readonly LiveSetting<float> GlowIntensity = Bind(InscribeSettings.Glow.Intensity);
        public static readonly LiveSetting<float> GlowRange = Bind(InscribeSettings.Glow.Range);
        public static readonly LiveSetting<int> GlowMaxLights = Bind(InscribeSettings.Glow.MaxLights);
        public static readonly LiveSetting<float> GlowRefreshSeconds = Bind(InscribeSettings.Glow.RefreshSeconds);
        public static readonly LiveSetting<bool> GlowStones = Bind(InscribeSettings.Glow.GlowRunes);
        public static readonly LiveSetting<bool> LootBeam = Bind(InscribeSettings.Glow.LootBeam);
        public static readonly LiveSetting<bool> LogRolls = new LiveSetting<bool>(() => false);
        public static readonly LiveSetting<bool> LogEffectRebuilds = new LiveSetting<bool>(() => false);
        // Wildblood capability integration is separate; never mistake another plugin's private ZDOs for its API.
        public static readonly LiveSetting<bool> EcrSynergy = new LiveSetting<bool>(() => false);
        internal static void Changed()
        {
            ColoredItemNames.Notify(); TooltipDetailLevel.Notify(); ShowDormantAffixes.Notify(); GroundGlow.Notify();
            GlowIntensity.Notify(); GlowRange.Notify(); GlowMaxLights.Notify(); GlowRefreshSeconds.Notify(); GlowStones.Notify(); LootBeam.Notify();
            Effects.EffectRuntime.MarkDirty();
            Loot.GearPool.Invalidate();   // Accessory Rarity changes which bases may drop rolled
        }
    }
}
