// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Kall Fimbulbringer's return to the Deep North boss room, run like Delve's own Queen return. The
// room is a ZoneSystem location (DN_Bossroom) without a dungeon generator or door, so it is found by its LocationProxy
// and is not a Delve.Dungeons.yml type: no Full or Resources mode applies to it, only the Kall settings. Each room's last
// visit lives in delve.kall.* keys on its proxy. Once due, with no phase of Kall alive in the room, nobody in it and no
// player holding its objects, Kall is placed asleep where the location put him (KallRoom.BossSpot).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Localization;
using ExtraPotions.Delve.Regrow;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    public sealed class DelveKallRoom : IDelveService
    {
        private const DelveKind Kind = DelveKind.Kall;
        private static readonly int proxyPrefab = KallRoom.Proxy.GetStableHashCode();
        private static readonly int location = KallRoom.Location.GetStableHashCode();
        private static readonly int boss = KallRoom.Boss.GetStableHashCode();
        private static readonly int player = "Player".GetStableHashCode();
        private static readonly HashSet<int> phases = new HashSet<int>(KallRoom.Phases.Select(p => p.GetStableHashCode()));
        private static readonly int visitDay = DelveKeys.VisitDay(Kind).GetStableHashCode();
        private static readonly int visitUtc = DelveKeys.VisitUtc(Kind).GetStableHashCode();

        private readonly Dictionary<ZDOID, Room> rooms = new Dictionary<ZDOID, Room>();
        private readonly WelcomeMemory welcomed = new WelcomeMemory();
        private DelveEngine? engine;

        private sealed class Room
        {
            public ZDOID Proxy;
            public Vector3 Position;
            public Vector3 Spot;
            public float Yaw;
            public Stamp? LastVisit;
            public bool VisitDirty;
            public string Id => Proxy.ToString();
        }

        public IReadOnlyList<DelveKind> Kinds { get; } = new[] { Kind };

        private DelveEngine E => engine ?? throw new InvalidOperationException("Delve's Kall room is not started.");

        private static Vec3 V(Vector3 v) => DelveWorld.ToVec(v);

        public void Start(DelveEngine e)
        {
            engine = e;
            rooms.Clear();
            Index();
            E.Log.Verbose("Delve " + Kind + ": " + rooms.Count + " Deep North boss rooms.");
        }

        public void Stop(DelveEngine e)
        {
            Flush();
            rooms.Clear();
            engine = null;
        }

        public void Check(DelveEngine e, ISet<DelveKind> sweeping)
        {
            if (!ReferenceEquals(engine, e)) return;
            if (!e.Active(Kind))
            {
                Flush();
                return;
            }
            Visit();
            if (sweeping.Contains(Kind)) Sweep();
            Flush();
        }

        /// <summary>Every generated boss room, by the proxy the game keeps for its location.</summary>
        private void Index()
        {
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
                if (zdo.GetPrefab() == proxyPrefab && zdo.GetInt(ZDOVars.s_location) == location) Add(zdo);
        }

        private void Add(ZDO proxy)
        {
            if (rooms.ContainsKey(proxy.m_uid)) return;
            float yaw = proxy.GetRotation().eulerAngles.y;
            var (spot, _) = KallRoom.BossSpot(V(proxy.GetPosition()), yaw);
            rooms[proxy.m_uid] = new Room
            {
                Proxy = proxy.m_uid, Position = proxy.GetPosition(), Spot = DelveWorld.ToVector(spot), Yaw = yaw,
                LastVisit = DelveKeys.Decode(proxy.GetFloat(visitDay), proxy.GetLong(visitUtc)),
            };
        }

        private Room? InsideOf(Vector3 p) => rooms.Values.FirstOrDefault(r => KallRoom.Inside(V(p), V(r.Spot)));

        private Room? EntranceOf(Vector3 p) => rooms.Values.FirstOrDefault(r => KallRoom.AtEntrance(V(p), V(r.Position)));

        private static List<ZDO> InRoom(Room room) =>
            DelveWorld.ObjectsNear(room.Spot, KallRoom.InsideRadius).Where(z => KallRoom.Inside(V(z.GetPosition()), V(room.Spot))).ToList();

        /// <summary>Every check: a player in the room restarts its clock; players at the entrance or inside are told once per trip.</summary>
        private void Visit()
        {
            var present = new HashSet<long>();
            foreach (var p in E.Players)
            {
                present.Add(p.Uid);
                var inside = InsideOf(p.Position);
                if (inside != null)
                {
                    var last = inside.LastVisit;
                    if (!last.HasValue || ClockRules.Elapsed(ClockKind.InGameDays, last.Value.CompletedWith(E.Now), E.Now) >= 0.01
                        || ClockRules.Elapsed(ClockKind.RealHours, last.Value.CompletedWith(E.Now), E.Now) >= 0.1)
                    {
                        inside.LastVisit = E.Now;
                        inside.VisitDirty = true;
                    }
                }
                var here = inside ?? EntranceOf(p.Position);
                if (here == null)
                {
                    welcomed.Leave(p.Uid);
                    continue;
                }
                if (welcomed.Enter(p.Uid, here.Id, inside != null ? 2 : 1)) Welcome(p, here, inside != null);
            }
            foreach (var gone in welcomed.Players.Where(uid => !present.Contains(uid)).ToList()) welcomed.Leave(gone);
        }

        /// <summary>Tell a player at or in a room whose Kall is gone when he returns.</summary>
        private void Welcome(DelvePlayer p, Room room, bool inside)
        {
            if (!DelveNotices.On || InRoom(room).Any(z => phases.Contains(z.GetPrefab()))) return;
            var (clock, delay) = E.ClockOf(Kind);
            int units = DungeonRegrowRules.UnitsLeft(room.LastVisit, E.Now, clock, delay, inside);
            var notice = inside ? SiteNotice.Inside : units > 0 ? SiteNotice.Entrance : SiteNotice.Due;
            var key = NoticeText.Site(Kind, notice);
            DelveNotices.Tell(p, notice == SiteNotice.Due ? DelveText.ServerText(key) : DelveText.ServerFormat(key, DelveNotices.Duration(clock, units)));
        }

        /// <summary>A player in the room, a player object there, or a player holding the proxy or anything in the room.</summary>
        private bool Occupied(Room room, List<ZDO> objects) =>
            E.Players.Any(p => KallRoom.Inside(V(p.Position), V(room.Spot)))
            || objects.Any(z => z.GetPrefab() == player || DelveWorld.HeldByPlayer(z))
            || DelveWorld.HeldByPlayer(ZDOMan.instance.GetZDO(room.Proxy));

        /// <summary>At dawn (day clock), every check (real hours) or on regenerate now.</summary>
        private void Sweep()
        {
            Index();
            bool dryRun = E.DryRun(Kind);
            bool now = E.RegenerateNow(Kind);
            var (clock, delay) = E.ClockOf(Kind);
            int tracked = 0, waiting = 0, deferred = 0, acted = 0;
            var blocked = new Dictionary<GuardResult, int>();
            foreach (var room in rooms.Values.ToList())
            {
                if (ZDOMan.instance.GetZDO(room.Proxy) == null)
                {
                    rooms.Remove(room.Proxy);
                    continue;
                }
                tracked++;
                var objects = InRoom(room);
                bool alive = objects.Any(z => phases.Contains(z.GetPrefab()));
                var outcome = KallRoom.Decide(room.LastVisit, E.Now, clock, delay, now, alive, Occupied(room, objects));
                if (outcome == DungeonOutcome.Regenerate && E.Settings.Get(DelveSettings.General.PlayerBaseGuard)
                    && DelveBases.Near(room.Position).Any(a => a.Contains(V(room.Position))))
                {
                    Count(blocked, GuardResult.PlayerBase);
                    continue;
                }
                switch (outcome)
                {
                    case DungeonOutcome.FirstSight:
                        room.LastVisit = E.Now;
                        room.VisitDirty = true;
                        continue;
                    case DungeonOutcome.Waiting:
                        waiting++;
                        continue;
                    case DungeonOutcome.Occupied:
                        Count(blocked, GuardResult.PlayerNear);
                        if (now)
                        {
                            // Regenerate-now could not reach it: make it due so the first sweep that finds it free does it.
                            room.LastVisit = clock == ClockKind.RealHours ? Stamp.UtcOnly(E.Now.Utc.AddHours(-delay)) : Stamp.DayOnly(E.Now.Day - delay);
                            room.VisitDirty = true;
                        }
                        continue;
                    case DungeonOutcome.Regenerate:
                        break;
                    default:
                        continue; // Kall lives: the room is never touched
                }
                if (E.Budget <= 0)
                {
                    deferred++;
                    continue;
                }
                E.Budget--;
                acted++;
                bool placed = dryRun || DelveWorld.Create(boss, room.Spot, new Vector3(0f, (room.Yaw + KallRoom.BossYaw) % 360f, 0f)) != null;
                room.LastVisit = E.Now; // the clock starts again
                room.VisitDirty = true;
                E.Record(Kind, "Kall " + (placed ? "returned" : "could not be placed") + " to the boss room at (" + room.Position.x.ToString("0") + ", "
                    + room.Position.z.ToString("0") + ")" + (now ? " (regenerate now)" : ""), dryRun);
            }
            E.Swept(Kind, deferred);
            E.Report(Kind, new KindReport(tracked, waiting, deferred, acted, blocked, true, dryRun, null));
        }

        private static void Count(Dictionary<GuardResult, int> map, GuardResult g) => map[g] = map.TryGetValue(g, out var n) ? n + 1 : 1;

        /// <summary>Writes pending visit stamps to each proxy, never while a player holds it.</summary>
        private void Flush()
        {
            foreach (var room in rooms.Values)
            {
                if (!room.VisitDirty) continue;
                ZDO zdo = ZDOMan.instance?.GetZDO(room.Proxy);
                if (zdo == null || DelveWorld.HeldByPlayer(zdo)) continue;
                zdo.Set(visitDay, DelveKeys.EncodeDay(room.LastVisit));
                zdo.Set(visitUtc, DelveKeys.EncodeUtc(room.LastVisit));
                room.VisitDirty = false;
            }
        }
    }
}
