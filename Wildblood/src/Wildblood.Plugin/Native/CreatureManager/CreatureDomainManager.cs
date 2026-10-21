// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using RootPlugin = ExtraPotions.Wildblood.Runtime.Plugin;
namespace ExtraPotions.Wildblood.AdoptedCm;
internal static class CreatureDomainManager
{
    internal static bool IsSynchronizedConfigurationReady() => RootPlugin.NativeRulesReady && NativeModifierRules.Ready;
}
