// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// What a dying raider pays, read on its own owner as it dies (features/raids.md section 5, "Coins and drops"): whether
    /// it pays at all, and the multiplier on its loot. Two things forfeit everything, coins and loot alike: a coin share
    /// below zero, the mark <see cref="RaiderKill"/> leaves on a raider it kills for a stop (a stop's kill can arrive
    /// before the host's "stopped" does), and its own raid having ended <see cref="RaidEnd.Stopped"/> at its host. A raid
    /// lost (robbed, abandoned, timed out) pays nothing more either (section 4, "Nothing else is paid at the end"): a
    /// raider still caught after it drops no coins - it leaves with them - and only its own loot, as any creature does.
    /// The multiplier is the one in the host's ZDO now, which falls when a stronger player arrives
    /// (<see cref="RaidDrops"/>). The raider's owner may not hold the host's ZDO (a raider that chased a player far out,
    /// or a host that has gone), so the raider also carries the multiplier as it was when it spawned
    /// (<see cref="Remember"/>), the fallback: never higher than the raid started with, at worst missing a later fall.
    /// </summary>
    public static class RaiderPurse
    {
        /// <summary>On the raider's ZDO: the raid's loot multiplier when it spawned, written beside its tag.</summary>
        public const string DropsAtSpawnKey = "ecr_raid_raider_drops";

        private static readonly int DropsAtSpawnHash = DropsAtSpawnKey.GetStableHashCode();

        /// <summary>Keeps the raid's multiplier on a freshly tagged raider, for an owner that cannot see the host when it
        /// dies. Raider's owner, in the frame it is tagged.</summary>
        public static void Remember(ZDO raider, float drops) => raider.Set(DropsAtSpawnHash, drops);

        /// <summary>True when the raider pays nothing at all: killed by a stop, or its raid was stopped.</summary>
        public static bool Forfeit(ZDO raider) => RaiderTag.Coins(raider) < 0 || EndedAs(raider) == RaidEnd.Stopped;

        /// <summary>True when its raid was lost before it died: it had left with its coins.</summary>
        public static bool Lost(ZDO raider) => IsLoss(EndedAs(raider));

        /// <summary>The multiplier on its loot: the host's now when this machine holds the host and it is still the same
        /// raid's (1 once that raid is lost), else the one it spawned with, else 1.</summary>
        public static float DropsFor(ZDO raider)
        {
            ZDO? host = SameRaidHost(raider);
            if (host == null)
            {
                return raider.GetFloat(DropsAtSpawnHash, 1f);
            }
            RaidState state = new RaidState(host);
            return IsLoss(state.Ended) ? 1f : state.Drops;
        }

        /// <summary>
        /// The raider starts walking off (its raid lost, its host gone or never seen): it takes its coins with it, and
        /// should a player still catch it, it drops its own loot unmultiplied. A won raid's straggler keeps both, and a
        /// stop's mark stays. Raider's owner, as the walk begins (<see cref="RaiderLeave"/>).
        /// </summary>
        public static void WalkOff(ZDO raider)
        {
            if (RaiderTag.Coins(raider) < 0 || EndedAs(raider) == RaidEnd.Won)
            {
                return;
            }
            RaiderTag.ClearCoins(raider);
            Remember(raider, 1f);
        }

        private static bool IsLoss(RaidEnd how) =>
            how == RaidEnd.Robbed || how == RaidEnd.Abandoned || how == RaidEnd.TimedOut;

        // How its own raid ended, read at the host; None while it runs, or when this machine does not hold its host.
        private static RaidEnd EndedAs(ZDO raider)
        {
            ZDO? host = SameRaidHost(raider);
            return host != null ? new RaidState(host).Ended : RaidEnd.None;
        }

        // The host's ZDO when this machine holds it and its raid (running or ended) is the one this raider came for; a
        // host that has since sounded another raid is not this raider's any more.
        private static ZDO? SameRaidHost(ZDO raider)
        {
            ZDOID hostId = RaiderTag.Host(raider);
            ZDO? host = hostId.IsNone() || ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(hostId);
            return host != null && new RaidState(host).StartedAt == RaiderTag.Raid(raider) ? host : null;
        }
    }
}
