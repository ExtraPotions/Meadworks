// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/WorldObjects.cs Create, Remove and HeldByPlayer, src/Regrow.cs BuiltNear). The MIT notice is in
// THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: the held rule is Delve's portable WorldRules.HeldByPlayer; the clearance search returns the
// positions of player-built objects for Delve's shared guards and searches as many zones as the radius needs; positions
// convert to Delve.Core's Vec3; the players list includes the single-player or host player.
using System.Collections.Generic;
using ExtraPotions.Delve.Engine;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    /// <summary>A player the engine sees: routed peer id, character name, reference position.</summary>
    public readonly struct DelvePlayer
    {
        public DelvePlayer(long uid, string name, Vector3 position, ZNetPeer? peer)
        {
            Uid = uid;
            Name = name;
            Position = position;
            Peer = peer;
        }

        public long Uid { get; }
        public string Name { get; }
        public Vector3 Position { get; }

        /// <summary>Null for the local player of single player or a host.</summary>
        public ZNetPeer? Peer { get; }
    }

    /// <summary>Creating and removing world objects from the authority, and the facts the guards read from the world.</summary>
    public static class DelveWorld
    {
        private static readonly List<ZDO> nearby = new List<ZDO>();

        public static Vec3 ToVec(Vector3 v) => new Vec3(v.x, v.y, v.z);

        public static Vector3 ToVector(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>
        /// The same steps ZNetView.Awake takes for an object that has no ZDO yet: CreateNewZDO, then Persistent, Type and
        /// Distant from the prefab's ZNetView, prefab and rotation. Null when the prefab is unknown.
        /// </summary>
        public static ZDO? Create(int prefabHash, Vector3 position, Vector3 eulerRotation)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabHash);
            ZNetView view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
            if (view == null) return null;
            ZDO zdo = ZDOMan.instance.CreateNewZDO(position, prefabHash);
            zdo.Persistent = view.m_persistent;
            zdo.Type = view.m_type;
            zdo.Distant = view.m_distant;
            zdo.SetPrefab(prefabHash);
            zdo.SetRotation(Quaternion.Euler(eulerRotation));
            return zdo;
        }

        /// <summary>ZDOMan.DestroyZDO only acts for the owner; the authority takes ownership first. Every client is told.</summary>
        public static void Remove(ZDO zdo)
        {
            if (zdo == null) return;
            zdo.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(zdo);
        }

        /// <summary>A connected player whose active area covers the object owns it (design B4; stale owners do not count).</summary>
        public static bool HeldByPlayer(ZDO? zdo)
        {
            if (zdo == null || ZNet.instance == null) return false;
            long owner = zdo.GetOwner();
            bool hasOwner = zdo.HasOwner();
            bool ownerIsServer = owner == ZDOMan.GetSessionID();
            if (!hasOwner || ownerIsServer) return WorldRules.HeldByPlayer(hasOwner, ownerIsServer, false, false);
            ZNetPeer peer = ZNet.instance.GetPeer(owner);
            return WorldRules.HeldByPlayer(true, false, peer != null, peer != null && ZNetScene.InActiveArea(zdo.GetPosition(), peer.GetRefPos()));
        }

        /// <summary>Every player in the world now: connected peers, plus the local player of single player or a host.</summary>
        public static List<DelvePlayer> Players()
        {
            var players = new List<DelvePlayer>();
            var net = ZNet.instance;
            if (net == null) return players;
            foreach (ZNetPeer peer in net.GetPeers())
                if (peer != null && peer.IsReady()) players.Add(new DelvePlayer(peer.m_uid, peer.m_playerName ?? "", peer.GetRefPos(), peer));
            if (!net.IsDedicated() && Player.m_localPlayer != null)
                players.Add(new DelvePlayer(ZNet.GetUID(), Player.m_localPlayer.GetPlayerName() ?? "", Player.m_localPlayer.transform.position, null));
            return players;
        }

        /// <summary>Positions of anything a player built (a ZDO with a creator) within <paramref name="radius"/> metres, flat, of the spot.</summary>
        public static List<Vec3> PlayerBuiltNear(Vector3 spot, float radius)
        {
            var built = new List<Vec3>();
            if (radius <= 0f || ZDOMan.instance == null) return built;
            foreach (ZDO zdo in ObjectsNear(spot, radius))
            {
                Vector3 p = zdo.GetPosition();
                if (zdo.GetLong(ZDOVars.s_creator) != 0L && new Vector2(p.x - spot.x, p.z - spot.z).magnitude < radius)
                    built.Add(ToVec(p));
            }
            return built;
        }

        /// <summary>Objects in the zones a circle of this radius around the spot touches (flat).</summary>
        public static List<ZDO> ObjectsNear(Vector3 spot, float radius)
        {
            nearby.Clear();
            int reach = Mathf.Max(1, Mathf.CeilToInt(radius / ZoneSystem.c_ZoneSize));
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(spot), new SimulationDistance(reach, 0, true), nearby);
            return new List<ZDO>(nearby);
        }
    }
}
