// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// What happens to the raiders when a raid ends (features/raids.md, "How it ends"), sent from the host's owner to
    /// each raider's owner - which may be another client - since only an owner may kill or move its creature. A stop
    /// kills every raider where it stands, dropping nothing (<see cref="RaiderKill"/>); a lost raid (robbed, abandoned,
    /// timed out) sends them off to vanish with their coins (<see cref="RaiderLeave"/>). The host's owner gives the order
    /// to its own raiders at once and sends one routed RPC to each other machine owning one, found by the raiders' ZDOs
    /// (<see cref="RaiderZdos"/>), carrying the host and the raid; each obeys for the raiders it owns. Neither order is
    /// the only way the news travels: every raider reads its raid at the host's ZDO on its owner every 1.5 seconds
    /// (<see cref="RaiderSteering"/>), so one the order missed - beyond what the host's owner was sent, or handed to a
    /// new owner meanwhile - dies or walks off within a couple of seconds all the same. What raiders go for while the raid
    /// runs is <see cref="RaiderSteering"/>'s, not called from here.
    /// </summary>
    public static class RaiderOrders
    {
        /// <summary>Routed to the owners of a lost raid's raiders (host ZDOID, raid id); each sends its own
        /// off.</summary>
        public const string LeaveRpc = "ecr_raid_leave";

        private static readonly List<long> Owners = new List<long>();

        /// <summary>
        /// Registers the stop (<see cref="RaidKeys.StopRaidersRpc"/>) and the walk off (<see cref="LeaveRpc"/>), each
        /// carrying the host's ZDOID and the raid's id, on this bus. Called on every machine at world start, once per bus
        /// (<see cref="RaidNet.EnsureRegistered"/>).
        /// </summary>
        public static void Register(ZRoutedRpc bus)
        {
            bus.Register<ZDOID, long>(RaidKeys.StopRaidersRpc, OnStop);
            bus.Register<ZDOID, long>(LeaveRpc, OnLeave);
        }

        /// <summary>
        /// The raid at <paramref name="runner"/> was stopped (already written as ended in its ZDO): each raider's owner
        /// kills it where it stands, with its death effect, dropping nothing, its coins included. Host's owner.
        /// </summary>
        public static void Stop(RaidRunner runner) => Send(runner, RaidKeys.StopRaidersRpc, kill: true);

        /// <summary>
        /// The raid at <paramref name="runner"/> was lost (<see cref="RaidEnd.Robbed"/>, <see cref="RaidEnd.Abandoned"/>
        /// or <see cref="RaidEnd.TimedOut"/>; already written in its ZDO): its raiders walk off and vanish with their
        /// coins, the same way whichever loss it was. Host's owner.
        /// </summary>
        public static void Disband(RaidRunner runner, RaidEnd how) => Send(runner, LeaveRpc, kill: false);

        private static void Send(RaidRunner runner, string rpc, bool kill)
        {
            ZDOID host = runner.HostId;
            if (host.IsNone())
            {
                return;
            }
            long raid = runner.State.StartedAt;
            RaiderRoster.ForRaid(host, raid, kill); // this machine's own raiders, with no round trip
            RaiderZdos.Owners(runner.Position, host, raid, Owners);
            if (ZRoutedRpc.instance != null)
            {
                foreach (long owner in Owners)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(owner, rpc, host, raid);
                }
            }
            Owners.Clear();
        }

        // Each raider's order is guarded on its own inside ForRaid, so nothing here can throw into the RPC bus.
        private static bool Allowed(long sender,ZDOID host,long raid,RaidEnd? end) { var zdo=ZDOMan.instance?.GetZDO(host); if(zdo==null||!zdo.HasOwner()||sender!=zdo.GetOwner())return false;var s=new RaidState(zdo);return s.StartedAt==raid&&s.Phase==RaidPhase.Ended&&(end.HasValue?s.Ended==end.Value:s.Ended==RaidEnd.Robbed||s.Ended==RaidEnd.Abandoned||s.Ended==RaidEnd.TimedOut); }
        private static void OnStop(long sender, ZDOID host, long raid) { if (Allowed(sender, host, raid, RaidEnd.Stopped)) RaiderRoster.ForRaid(host, raid, kill: true); }

        private static void OnLeave(long sender, ZDOID host, long raid) { if (Allowed(sender, host, raid, null)) RaiderRoster.ForRaid(host, raid, kill: false); }
    }
}
