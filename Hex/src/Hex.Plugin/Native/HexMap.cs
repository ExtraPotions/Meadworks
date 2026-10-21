// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexMap
{
    internal static void Initialize()
    {
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Map.Reveal", MischiefCategory.Information);
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Map.Reset", MischiefCategory.Information);
    }

    internal static string Execute(string[] args)
    {
        var reveal = args.Length == 2 && args[1].Equals("reveal", StringComparison.OrdinalIgnoreCase);
        var reset = args.Length >= 2 && args[1].Equals("reset", StringComparison.OrdinalIgnoreCase);
        if (!reveal && !reset || reset && (args.Length > 3 || args.Length == 3 && !args[2].Equals("confirm", StringComparison.OrdinalIgnoreCase)))
            return HexText.Text("map_help");
        if (!Prepare(reveal, out var map, out var reason)) return reason;
        if (reset && args.Length != 3) return HexText.Text("map_confirm_reset");
        try
        {
            // Native ExploreAll calls the texel overload, outside Cairn's natural-exploration XP scope.
            // Native Reset clears both local and received fog; neither operation removes pins or sends shared data.
            if (reveal) map.ExploreAll();
            else map.Reset();
        }
        catch { return HexText.Text("map_operation_failed"); }
        try { map.SaveMapData(); }
        catch { return HexText.Text("map_save_failed"); }
        return HexText.Text(reveal ? "map_revealed" : "map_reset_done");
    }
    internal static bool PrepareReset(out string reason) => Prepare(false, out _, out reason);
    static bool Prepare(bool reveal, out Minimap map, out string reason)
    {
        map = null!;
        reason = HexText.Text("map_unavailable");
        if (!Plugin.Ready || !Plugin.Active || Plugin.Settings?.Enabled.Value != true
            || Plugin.Settings.MapTools.Value != true || !HexMovement.Owned(Player.m_localPlayer)
            || !MeadworksHeadless.AllowsUi) return false;
        if (!MeadworksMischief.TryUse(Plugin.Guid, reveal ? "Map.Reveal" : "Map.Reset", MischiefCategory.Information,
            HexText.Text(reveal ? "map_reveal" : "map_reset"))) { reason = HexText.Text("actions_denied"); return false; }
        map = Minimap.instance;
        var game = Game.instance;
        if (map == null || game == null || game.GetPlayerProfile() == null || !map.m_hasGenerated
            || map.m_fogTexture == null || map.m_explored == null || map.m_exploredOthers == null)
            return false;
        return true;
    }

}
