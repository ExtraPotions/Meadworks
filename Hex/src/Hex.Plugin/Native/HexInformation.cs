// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexInformation
{
    static readonly CapabilityLink<Func<string[], bool>> thing = new CapabilityLink<Func<string[], bool>>(AdminCommandContract.Id, AdminCommandContract.ContractVersion);
    internal static void Initialize() => MeadworksMischief.Cheats.Tag(Plugin.Guid, "Debug.Seed", MischiefCategory.Information);

    /// <summary>The seed row sits on the Map page; Debug Tools keeps opening it as before.</summary>
    internal static bool SeedPage => Plugin.Settings != null && (Plugin.Settings.MapTools.Value || Plugin.Settings.DebugTools.Value);

    internal static string Seed()
    {
        if (!Plugin.Ready || !Plugin.Active || Plugin.Settings?.Enabled.Value != true
            || !SeedPage || !HexMovement.Owned(Player.m_localPlayer))
            return HexText.Text("seed_tools_required");
        if (!MeadworksMischief.TryUse(Plugin.Guid, "Debug.Seed", MischiefCategory.Information, HexText.Text("seed_label")))
            return HexText.Text("actions_denied");
        var net = ZNet.instance;
        if (net == null) return HexText.Text("seed_unavailable");
        if (net.IsServer())
        {
            var world = ZNet.GetWorldIfIsHost();
            return world == null ? HexText.Text("seed_unavailable") : HexText.Format("seed_value", world.m_seedName);
        }
        return TryThingCommand(new[] { "seed" }) ? HexText.Text("seed_thing_result") : HexText.Text("seed_thing_required");
    }
    // Thing's admin command (meadworks.admin.command) keeps Thing's server admin/module/audit checks.
    internal static bool TryThingCommand(string[] args) =>
        thing.TryGet(CruciblePlugin.IsReady ? CruciblePlugin.Services.Capabilities : null, out var run) && AdminCommandContract.TryRun(run, args);
}
