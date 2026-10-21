// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ExtraPotions.Hex.Runtime.Native;

namespace ExtraPotions.Hex.Runtime;

internal static class HexActionShortcuts
{
    internal static void Run(string key)
    {
        if (!Plugin.Ready || !Plugin.Active || Plugin.Settings?.Enabled.Value != true || !HexMovement.Owned(Player.m_localPlayer)) return;
        // Each existing producer rechecks its page preference and current C13 grant at dispatch.
        var result = key == "reset_speeds" ? Plugin.Settings.ResetSpeeds() : key == "reset" ? HexWindow.ConfirmMapReset()
            : key == "seed" ? HexInformation.Seed()
            : key == "poi" ? HexPoi.Request()
            : key == "estimatepoi" ? HexPoi.RequestEstimate()
            : key == "spawn_protection" ? HexSpawnProtection.Toggle()
            : key == "reveal" ? HexMap.Execute(new[] { "map", "reveal" })
            : HexActionKeys.Powers.Contains(key) ? HexActions.Execute(new[] { "power", key })
            : key == "heal" || key == "rested" ? HexActions.Execute(new[] { key }) : "";
        if (result.Length > 0 && MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, result);
    }
}
