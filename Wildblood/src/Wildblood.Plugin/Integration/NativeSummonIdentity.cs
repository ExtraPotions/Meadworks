// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ExtraPotions.Wildblood.Runtime;

// Native world loading renumbers ZDOIDs. Only a persisted alias can identify an already tracked object afterward.
internal static class NativeSummonIdentity
{
    internal const string Key = "wildblood.summon.identity";
    static readonly Dictionary<string, ZDO> aliases = new(StringComparer.Ordinal);
    static readonly Dictionary<string, ZDOID> lastSeen = new(StringComparer.Ordinal);
    static readonly HashSet<string> fresh = new(StringComparer.Ordinal);
    internal static string Raw(ZDOID id) => id.UserID.ToString(CultureInfo.InvariantCulture) + ":" + id.ID.ToString(CultureInfo.InvariantCulture);
    internal static string Id(ZDOID id)
    {
        var current = ZDOMan.instance.GetZDO(id);
        string alias = current?.GetString(Key) ?? "";
        return alias.Length == 0 ? Raw(id) : alias;
    }
    internal static string Enlist(ZDO zdo)
    {
        string alias = zdo.GetString(Key);
        if (alias.Length == 0)
        {
            alias = Raw(zdo.m_uid); Register(alias, zdo);
            zdo.Set(Key, alias); fresh.Add(alias); return alias;
        }
        Register(alias, zdo);
        return alias;
    }
    static void Register(string alias, ZDO zdo)
    {
        var parts = alias.Split(':');
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint sequence) || sequence == 0)
            throw new InvalidDataException("Invalid persisted summon identity.");
        if (aliases.TryGetValue(alias, out var previous) && !ReferenceEquals(previous, zdo)
            && ReferenceEquals(ZDOMan.instance.GetZDO(previous.m_uid), previous) && previous.GetString(Key) == alias)
            throw new InvalidDataException("Conflicting persisted summon identities.");
        aliases[alias] = zdo; lastSeen[alias] = zdo.m_uid;
    }
    internal static void Scan()
    {
        foreach (var zdo in ZDOMan.instance.m_objectsByID.Values)
        {
            string alias = zdo.GetString(Key);
            if (alias.Length != 0) Register(alias, zdo);
        }
    }
    internal static ZDO? Find(string alias)
    {
        if (!aliases.TryGetValue(alias, out var zdo)) return null;
        return ReferenceEquals(ZDOMan.instance.GetZDO(zdo.m_uid), zdo) && zdo.GetString(Key) == alias ? zdo : null;
    }
    internal static bool Fresh(string alias) => fresh.Contains(alias);
    internal static bool Destroyed(string alias) => lastSeen.TryGetValue(alias, out var id)
        && ZDOMan.instance.GetZDO(id) == null && ZDOMan.instance.m_deadZDOs.ContainsKey(id);
    internal static void Reset() { aliases.Clear(); lastSeen.Clear(); fresh.Clear(); }
}
