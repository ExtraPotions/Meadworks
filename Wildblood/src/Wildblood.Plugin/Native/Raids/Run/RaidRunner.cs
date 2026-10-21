// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The raid's engine, on every raid host: a Raiders Chest (<see cref="ChestPrefab"/> puts it on the piece) or the
    /// invisible marker of a test raid (<see cref="RaidMarker"/>), so both share all of the raid's code. It keeps nothing
    /// of the raid itself: <see cref="RaidState"/> reads it from the host's ZDO. On the host's owner the shared clock
    /// (<see cref="RaidTicker"/>) runs <see cref="RaidPhases"/> a few times a second; on every other machine it does
    /// nothing but answer the HUD line and pass on a stop or a player's presence, and the moment the ZDO's ownership
    /// moves here the next tick carries the raid on from the ZDO alone. Live runners are listed in
    /// <see cref="RaidHosts"/>, so nothing ever searches the scene for one.
    /// </summary>
    public sealed class RaidRunner : MonoBehaviour
    {
        /// <summary>Set on the test marker's prefab: a few seconds after its raid is over (at once when it loads with
        /// none), the host removes itself.</summary>
        public bool RemoveWhenDone;

        private ZNetView _nview = null!;
        private bool _live;
        private bool _faultReported;

        /// <summary>The players this owner heard from (<see cref="RaidHere"/>), beside those it sees.</summary>
        internal RaidHere.Reports Heard { get; } = new RaidHere.Reports();

        public ZNetView View => _nview;

        /// <summary>The host's ZDO; null on a placement ghost or once the host is gone.</summary>
        public ZDO? Zdo => _live && _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;

        /// <summary>The raid in the host's ZDO. Only while <see cref="Zdo"/> is not null.</summary>
        public RaidState State => new RaidState(_nview.GetZDO());

        /// <summary>The host's id: what every raider's tag names.</summary>
        public ZDOID HostId => Zdo?.m_uid ?? ZDOID.None;

        public Vector3 Position => transform.position;

        /// <summary>The base's biome, which raiders roll their stars and mutations in. The same on every machine.</summary>
        public Heightmap.Biome Biome { get; private set; }

        public bool IsOwner => Zdo != null && _nview.IsOwner();

        /// <summary>The band the raid's heat fell in.</summary>
        public RaidBand Band => RaidTable.Band(State.Band);

        /// <summary>Living players within the raid's radius at the owner's last tick (seen or heard from).</summary>
        public int PlayersNear { get; private set; }

        private void Awake() => Guard.Run("RaidRunner.Awake", Setup);

        private void Setup()
        {
            _nview = GetComponent<ZNetView>();
            if (_nview == null || !_nview.IsValid())
            {
                return; // a placement ghost: no ZDO, no raid
            }
            _live = true;
            Biome = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(transform.position) : Heightmap.Biome.None;
            _nview.Register(RaidKeys.StopAskRpc, OnStopAsk);
            _nview.Register<int>(RaidKeys.HereRpc, OnHere);
            RaidHosts.Add(this);
        }

        private void OnDestroy() => RaidHosts.Remove(this);

        /// <summary>The shared clock's tick. Owner only; one failure is logged, and the raid ticks on.</summary>
        internal void Tick(long now)
        {
            if (!IsOwner || !ExtraPotions.Wildblood.Runtime.NativeRaidAdmission.CanRun(this))
            {
                return;
            }
            try
            {
                RaidPhases.Step(this, now);
            }
            catch (Exception e)
            {
                ReportOnce(e);
            }
        }

        /// <summary>How many players the owner's survey found this tick, kept for the next wave's size
        /// (<see cref="RaidWaves.Begin"/>).</summary>
        internal void Remember(RaidPresence.Count near) => PlayersNear = near.Players;

        /// <summary>Ends the raid now, however it ended. Owner only; nothing when no raid runs.</summary>
        public void End(RaidEnd how) => RaidFinish.End(this, how);

        /// <summary>Removes a test marker from the world. Owner only.</summary>
        internal void Remove()
        {
            if (IsOwner && ZNetScene.instance != null)
            {
                ZNetScene.instance.Destroy(gameObject);
            }
        }

        private void OnStopAsk(long sender) => Guard.Run("RaidRunner.OnStopAsk", () =>
        {
            if (sender == ZNet.GetUID() && IsOwner && State.Running)
            {
                End(RaidEnd.Stopped);
            }
        });

        private void OnHere(long sender, int tier) =>
            Guard.Run("RaidRunner.OnHere", () => { foreach(var player in Player.GetAllPlayers()) if(player&&player.m_nview&&player.m_nview.IsValid()&&player.m_nview.GetZDO().GetOwner()==sender&&!player.IsDead()&&(player.transform.position-Position).sqrMagnitude<=RaidTable.RaidRadius*RaidTable.RaidRadius){Heard.Note(sender,tier,Time.time);break;} });

        private void ReportOnce(Exception e)
        {
            if (!_faultReported)
            {
                _faultReported = true;
                Guard.Report(e, $"raid at {Position:F0}");
            }
        }
    }
}
