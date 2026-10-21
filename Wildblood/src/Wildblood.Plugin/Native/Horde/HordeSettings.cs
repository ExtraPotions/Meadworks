// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
namespace ExtraPotions.Wildblood.Adopted.Horde;
internal static class HordeSettings
{
    internal static bool Available => Plugin.NativeRulesReady && NativeModifierRules.Ready
        && WildbloodModules.ProductActive(out _) && Plugin.Settings != null
        && Plugin.Settings.Get(WildbloodSettings.General.Enabled)
        && !WildbloodModules.StepAside.SteppedAside(WildbloodModule.Horde);
    internal static bool Enabled => Available && WildbloodModules.Active(WildbloodModule.Horde);
    internal static float Spawns => Plugin.Settings?.Get(WildbloodSettings.Horde.SpawnMultiplier) ?? 2f;
    internal static float Health => Plugin.Settings?.Get(WildbloodSettings.Horde.HealthMultiplier) ?? .65f;
    internal static float Attack => Plugin.Settings?.Get(WildbloodSettings.Horde.AttackMultiplier) ?? .75f;
}
