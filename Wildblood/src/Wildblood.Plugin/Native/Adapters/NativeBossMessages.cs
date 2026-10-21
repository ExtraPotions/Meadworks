// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Runtime;

internal static class NativeBossMessages
{
    internal static bool Active => Plugin.NativeRulesReady && WildbloodModules.Active(WildbloodModule.Bosses)
        && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId != 0
        && ZNet.instance != null && ZNet.m_world != null && ZNet.instance.GetWorldUID() == CruciblePlugin.Session.WorldId;
    internal static bool Current(ZRoutedRpc? registered, NativeRpcOwner owner) => Active && registered != null
        && ReferenceEquals(registered, ZRoutedRpc.instance) && NativeRpcOwnership.OwnsRouted(registered, owner);
    internal static bool Known(long sender) => ZNet.instance != null && (sender == ZDOMan.GetSessionID()
        || ZNet.instance.GetPeer(sender)?.IsReady() == true);
    internal static bool Server(long sender) => ZRoutedRpc.instance != null && sender == ZRoutedRpc.instance.GetServerPeerID();
    internal static bool Finite(Vector3 value) => float.IsNaN(value.x) == false && float.IsInfinity(value.x) == false
        && float.IsNaN(value.y) == false && float.IsInfinity(value.y) == false
        && float.IsNaN(value.z) == false && float.IsInfinity(value.z) == false;
    internal static bool Source(ZDOID id, long owner, out ZDO zdo)
    {
        zdo = ZDOMan.instance?.GetZDO(id)!;
        var prefab = zdo != null ? ZNetScene.instance?.GetPrefab(zdo.GetPrefab()) : null;
        var character = prefab != null ? prefab.GetComponent<Character>() : null;
        return id != ZDOID.None && zdo != null && zdo.m_uid == id && zdo.GetOwner() == owner
            && character != null && !character.IsPlayer() && character.IsBoss();
    }
}
