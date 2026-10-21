// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// What plunderers go for at one raid's base, as this machine has it loaded (features/raids.md section 4): the chests
    /// - any container piece a player built within the raid's 96 m of the host - and, for when none is left, the crafting
    /// stations and station upgrades there. Not a raid host (the Raiders Chest, another chest's, a test marker), not a
    /// cart's or a ship's hold (their container sits under the vehicle, not on a piece of its own), not a world chest
    /// nobody built. Built from the game's own list of loaded pieces, shared by every raider of the raid on this machine,
    /// and rebuilt at most every 5 seconds: a piece built meanwhile waits for the next rebuild, a destroyed one reads as
    /// null and is skipped. Which prefabs are chests or stations is decided once per prefab.
    /// </summary>
    internal sealed class BaseTargets
    {
        private const float RefreshSeconds = 5f;

        /// <summary>Seconds after a raid's raiders last asked before its lists are dropped.</summary>
        private const float ForgetSeconds = 60f;

        private enum Kind : byte
        {
            None,
            Chest,
            Station,
        }

        private static readonly List<BaseTargets> Bases = new List<BaseTargets>();
        private static readonly Dictionary<int, Kind> Kinds = new Dictionary<int, Kind>();
        private static readonly List<Piece> Near = new List<Piece>();

        private readonly ZDOID _host;
        private readonly List<StaticTarget> _chests = new List<StaticTarget>();
        private readonly List<StaticTarget> _stations = new List<StaticTarget>();
        private float _builtAt = float.MinValue;
        private float _askedAt;

        private BaseTargets(ZDOID host) => _host = host;

        /// <summary>The base around this host, fresh to within 5 seconds; null when this machine holds no ZDO for the host.</summary>
        public static BaseTargets? For(ZDOID host)
        {
            ZDO? zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(host) : null;
            if (zdo == null)
            {
                return null;
            }
            float now = Time.time;
            BaseTargets found = Find(host, now);
            found._askedAt = now;
            if (now - found._builtAt > RefreshSeconds)
            {
                found.Build(zdo.GetPosition(), now);
            }
            return found;
        }

        /// <summary>The nearest chest still standing; when none is, the nearest station; else null.</summary>
        public StaticTarget? Nearest(Vector3 from) => NearestIn(_chests, from) ?? NearestIn(_stations, from);

        // Usually one base, at most a few: a list walked whole, which also drops the ones nobody has asked about lately.
        private static BaseTargets Find(ZDOID host, float now)
        {
            BaseTargets? found = null;
            for (int i = Bases.Count - 1; i >= 0; i--)
            {
                BaseTargets entry = Bases[i];
                if (entry._host == host)
                {
                    found = entry;
                }
                else if (now - entry._askedAt > ForgetSeconds)
                {
                    Bases.RemoveAt(i);
                }
            }
            if (found == null)
            {
                found = new BaseTargets(host);
                Bases.Add(found);
            }
            return found;
        }

        private void Build(Vector3 at, float now)
        {
            _builtAt = now;
            _chests.Clear();
            _stations.Clear();
            Near.Clear();
            Piece.GetAllPiecesInRadius(at, RaidTable.RaidRadius, Near);
            foreach (Piece piece in Near)
            {
                Sort(piece);
            }
            Near.Clear();
        }

        private void Sort(Piece piece)
        {
            ZDO? zdo = piece.m_nview != null ? piece.m_nview.GetZDO() : null;
            if (zdo == null || zdo.m_uid == _host || !piece.IsPlacedByPlayer()
                || !string.IsNullOrEmpty(zdo.GetString(ExtraPotions.Wildblood.Runtime.NativeRaidChest.TokenKey)))
            {
                return;
            }
            Kind kind = KindOf(piece, zdo.GetPrefab());
            if (kind == Kind.Chest)
            {
                _chests.Add(piece);
            }
            else if (kind == Kind.Station)
            {
                _stations.Add(piece);
            }
        }

        // Every piece of one prefab carries the same components, so the first one met decides for all of them.
        private static Kind KindOf(Piece piece, int prefab)
        {
            if (Kinds.TryGetValue(prefab, out Kind kind))
            {
                return kind;
            }
            Container container = piece.GetComponent<Container>();
            if (container != null && container.m_rootObjectOverride == null)
            {
                kind = Kind.Chest;
            }
            else if (piece.GetComponent<CraftingStation>() != null || piece.GetComponent<StationExtension>() != null)
            {
                kind = Kind.Station;
            }
            Kinds[prefab] = kind;
            return kind;
        }

        private static StaticTarget? NearestIn(List<StaticTarget> targets, Vector3 from)
        {
            StaticTarget? best = null;
            float bestSqr = float.MaxValue;
            foreach (StaticTarget target in targets)
            {
                if (target == null)
                {
                    continue; // destroyed since the last rebuild
                }
                float sqr = (target.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = target;
                    bestSqr = sqr;
                }
            }
            return best;
        }
    }
}
