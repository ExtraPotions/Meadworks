// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Bootstrap;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    internal static class LoadedWeaponAvailability
    {
        // Upstream's continuity hooks are unconditional, even when its creature/loot switches are off. Edge owns weapon
        // handling: with Edge or ECR installed their hooks keep crossbows loaded and Wildblood's copy stays inert.
        internal static bool Active => Plugin.Ready && Plugin.Settings != null
            && Plugin.Settings.Get(WildbloodSettings.General.Enabled)
            && WildbloodModules.ProductActive(out _)
            && LoadedWeaponOwnership.WildbloodActs(Chainloader.PluginInfos.Keys);
    }
}
