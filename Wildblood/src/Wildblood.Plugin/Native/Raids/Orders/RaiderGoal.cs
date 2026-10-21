// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// A raider's building target (features/raids.md section 4): a chest raider goes for the Raiders Chest, a plunderer
    /// for the nearest chest of the base and, when none is left, the nearest crafting station or station upgrade
    /// (<see cref="BaseTargets"/>), and with neither left, the Raiders Chest after all. It keeps the piece it has while
    /// that stands and takes the next of its kind when it falls. A chest raider whose host is no piece - the invisible
    /// marker of a test raid - becomes a plunderer, in its tag. The piece's id is kept in the tag
    /// (<see cref="RaiderTag.SetTarget"/>), so a new owner goes on for the same one. Asked on the raider's owner only, on
    /// its slow tick.
    /// </summary>
    internal sealed class RaiderGoal
    {
        private StaticTarget? _target;
        private ZDOID _targetId = ZDOID.None;

        /// <summary>The piece to go for now; null when nothing of the base is loaded on this machine.</summary>
        public StaticTarget? Current(RaiderSteering raider)
        {
            if (_target != null)
            {
                return _target; // still standing (a destroyed piece reads as null)
            }
            StaticTarget? next = Pick(raider);
            Remember(raider.Zdo, next);
            return next;
        }

        /// <summary>Ownership has just arrived: go on for the piece the last owner wrote in the tag, if it still stands.</summary>
        public void Adopt(ZDO? zdo)
        {
            _targetId = zdo != null ? RaiderTag.Target(zdo) : ZDOID.None;
            ZNetScene scene = ZNetScene.instance;
            GameObject? piece = _targetId.IsNone() || scene == null ? null : scene.FindInstance(_targetId);
            _target = piece != null ? piece.GetComponent<StaticTarget>() : null;
        }

        private static StaticTarget? Pick(RaiderSteering raider)
        {
            GameObject? host = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(raider.Host) : null;
            StaticTarget? chest = host != null ? host.GetComponent<StaticTarget>() : null;
            ZDO? zdo = raider.Zdo;
            if (zdo != null && RaiderTag.Role(zdo) == RaiderRole.ChestRaider)
            {
                if (chest != null || host == null)
                {
                    return chest; // the chest; or, not loaded here, nothing yet: it walks toward the base until it is
                }
                RaiderTag.SetRole(zdo, RaiderRole.Plunderer); // a host that is no piece (a test marker): it plunders instead
            }
            // A base stripped of chests and stations leaves the plunderers the gold: the Raiders Chest, when it is a piece.
            return BaseTargets.For(raider.Host)?.Nearest(raider.transform.position) ?? chest;
        }

        private void Remember(ZDO? zdo, StaticTarget? target)
        {
            _target = target;
            ZNetView? view = target != null ? target.GetComponent<ZNetView>() : null;
            ZDOID id = view != null && view.IsValid() ? view.GetZDO().m_uid : ZDOID.None;
            if (zdo != null && id != _targetId)
            {
                _targetId = id;
                RaiderTag.SetTarget(zdo, id);
            }
        }
    }
}
