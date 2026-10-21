// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// A raider walking off when its raid is lost (features/raids.md, "How it ends": robbed, abandoned, timed out; and a
    /// raider that loads with no raid running behind it). It stops fighting and walks away from the base - the game's own
    /// flee, from the host, driven by <see cref="RaiderLeavePatch"/> in place of its thinking - and vanishes once no
    /// player is within 40 m (the game's own range for a creature walking off to despawn) or after 30 seconds, whichever
    /// comes first. Its coins go with it from the first step (<see cref="RaiderPurse.WalkOff"/>): one a player still
    /// catches drops only its own loot, as any creature. Vanishing is a plain despawn on its owner, with no death and so no
    /// drops. Only goods a Thieving raider stole from the players fall where it stood, as on any despawn of a thief
    /// (<see cref="Patches.ThievingDespawnPatch"/>). Kept in memory on the owner: a new owner reads the raid as over
    /// itself and starts its own walk off.
    /// </summary>
    internal sealed class RaiderLeave
    {
        /// <summary>The longest a raider walks off in sight of players before it vanishes anyway.</summary>
        private const float VanishSeconds = 30f;

        /// <summary>BaseAI.MoveAwayAndDespawn's own range: with no player this close, a creature walking off vanishes.</summary>
        private const float UnseenRange = 40f;

        private float _since = -1f;
        private Vector3 _from;

        /// <summary>True from the order to leave until it vanishes, on its owner.</summary>
        public bool Active => _since >= 0f;

        /// <summary>Stops it fighting and starts it walking away from <paramref name="from"/> (the host), its coins with
        /// it. Owner only.</summary>
        public void Begin(RaiderSteering raider, Vector3 from)
        {
            ZDO? zdo = raider.Zdo;
            if (zdo != null)
            {
                RaiderPurse.WalkOff(zdo);
            }
            MonsterAI ai = raider.Ai;
            ai.m_targetCreature = null;
            ai.m_targetStatic = null;
            ai.SetAlerted(false); // walks, rather than runs, until something hits it
            ai.SetHuntPlayer(false);
            _from = from;
            _since = Time.time;
            RaiderRoster.StartLeaving(raider);
        }

        /// <summary>Forgets the walk off: ownership has moved on, or it was tamed on its way out.</summary>
        public void Stop(RaiderSteering raider)
        {
            if (Active)
            {
                _since = -1f;
                RaiderRoster.StopLeaving(raider);
            }
        }

        /// <summary>One AI step of the walk: the game's flee from the host, re-picked every couple of seconds.</summary>
        public void Step(MonsterAI ai, float dt) => ai.Flee(dt, _from);

        /// <summary>On the slow tick: vanishes once out of every player's way, or once it has walked long enough.</summary>
        public void Check(RaiderSteering raider, float now)
        {
            Vector3 at = raider.transform.position;
            if (now - _since >= VanishSeconds || Player.GetClosestPlayer(at, UnseenRange) == null)
            {
                Vanish(raider, at);
            }
        }

        private void Vanish(RaiderSteering raider, Vector3 at)
        {
            ZDO? zdo = raider.Zdo;
            if (zdo == null)
            {
                return;
            }
            if (PouchStore.Count(zdo) > 0)
            {
                if (!PouchDrop.DropAll(PouchStore.Load(zdo), at, zdo.m_uid, zdo, lastChance: false)) return; // the players' stolen goods, never the raid's coins
            }
            Stop(raider);
            raider.View.Destroy();
        }
    }
}
