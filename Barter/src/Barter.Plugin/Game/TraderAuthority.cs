// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Barter.Lists;
using UnityEngine;

namespace ExtraPotions.Barter.Runtime
{
    /// <summary>Only saved-world instances and fresh server-created instances can acquire a trader identity.</summary>
    internal static class TraderAuthority
    {
        private sealed class Anchor { internal int Prefab; internal Vector3 Position; }
        private static readonly Dictionary<ZDOID, Anchor> anchors = new Dictionary<ZDOID, Anchor>();
        private static ZDOMan manager;

        private static bool HasTrader(ZDO zdo)
        {
            var prefab = zdo == null ? null : ZNetScene.instance?.GetPrefab(zdo.GetPrefab());
            return zdo != null && zdo.Persistent && prefab != null && prefab.GetComponent<Trader>() != null;
        }
        private static bool Allowed(ZDO zdo) => HasTrader(zdo) && Purses.HasPurse(TraderNames.Normalize(ZNetScene.instance.GetPrefab(zdo.GetPrefab()).name));

        internal static void CaptureWorld()
        {
            anchors.Clear(); manager = ZDOMan.instance;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || manager == null) return;
            foreach (ZDO zdo in manager.m_objectsByID.Values)
                if (HasTrader(zdo)) Remember(zdo);
            Plugin.Log?.Verbose("Trader authority captured " + anchors.Count + " saved-world instances. Remote distance checks follow the peer's character ZDO, not independently verified player movement.");
        }

        internal static void ServerCreated(ZDO zdo)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || zdo == null || !HasTrader(zdo) ||
                zdo.m_uid.UserID != ZDOMan.GetSessionID() || zdo.GetOwner() != ZDOMan.GetSessionID()) return;
            if (manager != ZDOMan.instance) { anchors.Clear(); manager = ZDOMan.instance; }
            Remember(zdo);
        }

        private static void Remember(ZDO zdo)
        {
            if (!anchors.ContainsKey(zdo.m_uid)) anchors.Add(zdo.m_uid, new Anchor { Prefab = zdo.GetPrefab(), Position = zdo.GetPosition() });
            if (Allowed(zdo)) zdo.SetOwner(ZDOMan.GetSessionID());
        }

        internal static bool Owns(ZDO zdo) => manager == ZDOMan.instance && zdo != null && anchors.ContainsKey(zdo.m_uid) && Allowed(zdo);

        internal static bool CanSpawn(long sender, int prefab)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || sender == ZDOMan.GetSessionID()) return true;
            // Native SpawnObject accepts a peer-supplied prefab and creates a fresh server-owned ZDO.
            // Refuse remote trader creation before it can masquerade as an authoritative server spawn.
            var candidate = ZNetScene.instance?.GetPrefab(prefab);
            return candidate == null || candidate.GetComponent<Trader>() == null || !Purses.HasPurse(TraderNames.Normalize(candidate.name));
        }

        internal static void Received(ZDO zdo)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || zdo == null) return;
            if (manager == ZDOMan.instance && anchors.TryGetValue(zdo.m_uid, out Anchor anchor))
            {
                var original = ZNetScene.instance?.GetPrefab(anchor.Prefab);
                bool originalAllowed = original != null && Purses.HasPurse(TraderNames.Normalize(original.name));
                if (originalAllowed || Allowed(zdo))
                {
                    // Peer updates cannot change a trusted trader's identity or turn a temporary move into a saved anchor.
                    zdo.SetPrefab(anchor.Prefab); zdo.SetPosition(anchor.Position); zdo.Persistent = true;
                    if (originalAllowed) zdo.SetOwner(ZDOMan.GetSessionID());
                    return;
                }
            }
            if (!Allowed(zdo)) return;
            // World loads use Load, not Deserialize. An unknown allowlisted trader arriving here was peer-created.
            zdo.Persistent = false;
            zdo.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(zdo);
        }

        internal static bool TryValidate(ZDO zdo, out Vector3 position)
        {
            position = default;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !Owns(zdo) || !Allowed(zdo) ||
                !anchors.TryGetValue(zdo.m_uid, out Anchor anchor) || anchor.Prefab != zdo.GetPrefab() ||
                zdo.GetOwner() != ZDOMan.GetSessionID() || !(Vector3.Distance(anchor.Position, zdo.GetPosition()) <= 0.1f)) return false;
            position = anchor.Position;
            return true;
        }
    }
}
