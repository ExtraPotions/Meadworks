// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Hex.Runtime.Native;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Hex.Runtime;

// Crucible owns the actual settings and Jotunn publication; Hex presents only that public configuration surface.
internal static class HexPolicy
{
    internal static bool Available => Plugin.Ready && Plugin.Active && CruciblePlugin.Session.AuthorityReady
        && HexMovement.Owned(Player.m_localPlayer) && !Player.m_localPlayer.IsDead()
        && MeadworksConfig.EditDecision(ConfigScope.ServerAuthoritative).Allowed;
    static ConfigEntry<T>? Entry<T>(string key)
    {
        if (!Available || !Chainloader.PluginInfos.TryGetValue(CruciblePlugin.Guid, out var info) || info.Instance == null) return null;
        return info.Instance.Config.TryGetEntry<T>(MischiefPolicy.Section, key, out var entry) ? entry : null;
    }
    internal static string SetAccess(MischiefCategory category, MischiefAccess value)
    {
        if (!Enum.IsDefined(typeof(MischiefCategory), category) || !Enum.IsDefined(typeof(MischiefAccess), value)) return HexText.Text("ui_policy_failed");
        return Amend(MischiefPolicy.SettingKey(category), value);
    }
    internal static string ValidatePlayers(string value)
    {
        if (value.Length > MischiefPlayers.MaxEntries * 98) return HexText.Text("ui_policy_invalid");
        MischiefPlayers.Parse(value, out var rejected);
        return rejected.Count == 0 ? "" : HexText.Text("ui_policy_invalid");
    }
    internal static string SetPlayers(string value)
    {
        if (ValidatePlayers(value).Length != 0) return HexText.Text("ui_policy_invalid");
        var players = MischiefPlayers.Parse(value, out _);
        return Amend(MischiefPolicy.ListedKey, players.Format());
    }
    static string Amend<T>(string key, T value)
    {
        var entry = Entry<T>(key);
        if (entry == null) return HexText.Text("ui_policy_unavailable");
        try
        {
            var result = MeadworksConfig.Amend(entry, CruciblePlugin.Guid, ConfigScope.ServerAuthoritative, value, key);
            return result.Applied ? HexText.Text("ui_policy_submitted") : MeadworksBlockedOptions.Reason(result.Decision.Reason);
        }
        catch { return HexText.Text("ui_policy_failed"); }
    }
}
