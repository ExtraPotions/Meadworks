// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx.Logging;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using RootPlugin = ExtraPotions.Wildblood.Runtime.Plugin;
namespace ExtraPotions.Wildblood.AdoptedCm;

// Read-through facade over the existing C01 settings. It owns no binder or plugin lifecycle.
internal static class CreatureManagerPlugin
{
    internal enum Toggle { On, Off }
    internal enum ModifierIconLayout { FixedCategorySlots, RightPacked }
    internal enum KarmaSystemMode { Off, KarmaLevelAndEnforcer, KarmaLevelOnly, EnforcerOnly }
    internal sealed class Setting<T>
    {
        readonly Func<T> read;
        internal Setting(Func<T> read) { this.read = read; }
        internal T Value => read();
    }
    internal static ManualLogSource Log => RootPlugin.NativeLogger ?? throw new InvalidOperationException("Wildblood logger is not ready.");
    static T Read<T>(WildbloodSetting<T> spec) => RootPlugin.Settings == null ? spec.Default : RootPlugin.Settings.Get(spec);
    static Setting<T> From<T>(WildbloodSetting<T> spec) => new(() => Read(spec));
    static Setting<Toggle> Flag(WildbloodSetting<bool> spec) => new(() => Read(spec) ? Toggle.On : Toggle.Off);
    static bool Active(WildbloodModule module) => RootPlugin.NativeRulesReady && NativeModifierRules.Ready && WildbloodModules.Active(module);
    static Setting<Toggle> OwnedFlag(WildbloodSetting<bool> spec, WildbloodModule module) => new(() => Active(module) && Read(spec) ? Toggle.On : Toggle.Off);
    internal static readonly Setting<int> ReflectionDamageCap = From(WildbloodSettings.Traits.ReflectionDamageCap);
    internal static readonly Setting<float> BlinkAlertGracePeriod = From(WildbloodSettings.Traits.BlinkAlertGrace);
    internal static readonly Setting<float> NormalCreatureNameplateRange = From(WildbloodSettings.Display.NameplateDistance);
    internal static readonly Setting<Toggle> ShowSneakHoverResistances = OwnedFlag(WildbloodSettings.Display.SneakInspect, WildbloodModule.Display);
    internal static readonly Setting<ModifierIconLayout> ModifierHudIconLayout = new(() => (ModifierIconLayout)Read(WildbloodSettings.Display.ModifierIconLayout));
    internal static readonly Setting<KarmaSystemMode> KarmaMode = new(() => Active(WildbloodModule.Pressure) ? (KarmaSystemMode)Read(WildbloodSettings.Pressure.KarmaMode) : KarmaSystemMode.Off);
    internal static readonly Setting<int> MaximumEnforcersPerSector = From(WildbloodSettings.Pressure.MaxEnforcersPerSector);
    internal static readonly Setting<int> EnforcerAbandonedDespawnSeconds = From(WildbloodSettings.Pressure.EnforcerAbandonDespawn);
    internal static readonly Setting<int> DungeonEnforcerSpawnDelay = From(WildbloodSettings.Pressure.DungeonEnforcerDelay);
    internal static readonly Setting<Toggle> BlockEnforcerWhileBossActive = Flag(WildbloodSettings.Pressure.BlockEnforcerWhileBossActive);
    internal static readonly Setting<Toggle> BlockKarmaGainWhileBossActive = Flag(WildbloodSettings.Pressure.BlockKarmaWhileBossActive);
    internal static readonly Setting<Toggle> BlockKarmaGainWhileEnforcerActive = Flag(WildbloodSettings.Pressure.BlockKarmaWhileEnforcerActive);
    internal static readonly Setting<Toggle> BlockOmenEnforcerDuringCooldown = Flag(WildbloodSettings.Pressure.BlockOmenDuringCooldown);
    internal static readonly Setting<Toggle> BlockNearbySpawnsWhileBossActive = OwnedFlag(WildbloodSettings.Bosses.BlockSpawnsNearBoss, WildbloodModule.Bosses);
    internal static readonly Setting<Toggle> BlockNearbySpawnsWhileEnforcerActive = OwnedFlag(WildbloodSettings.Pressure.BlockSpawnsNearEnforcer, WildbloodModule.Pressure);
    internal static readonly Setting<Toggle> ShowKarmaValueOnMinimap = OwnedFlag(WildbloodSettings.Display.ShowKarmaUnderMinimap, WildbloodModule.Display);
}
