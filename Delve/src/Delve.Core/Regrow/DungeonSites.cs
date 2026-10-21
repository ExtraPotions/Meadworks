// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/DungeonRegrow.cs DungeonAt, EntranceAt, FindDoors, BossSpot (the room data layout), the welcomed stages of Tick
// and the per-dungeon branch of Sweep). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: pure functions over positions and facts; interiors start at the game's own interior height
// (Character.InInterior, 3000 m) instead of 4000 m; the clock is a Delve two-clock stamp with a fractional delay instead
// of whole days; the decision names its outcome instead of counting it; SpotClear adds a per-spot build clearance
// (user decision R-Q2, 2026-10-08; RestockRegen has none inside dungeons).
using System;
using System.Collections.Generic;
using System.IO;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Regrow
{
    public static class DungeonSites
    {
        /// <summary>Character.InInterior: interiors are built above this height.</summary>
        public const float InteriorHeight = 3000f;

        /// <summary>A door belongs to the nearest dungeon within this many metres, flat.</summary>
        public const float DoorSearch = 100f;

        /// <summary>
        /// The dungeon of this kind whose interior holds <paramref name="p"/>: above interior height, the nearest of
        /// <paramref name="mine"/> flat within <paramref name="insideRadius"/> and 500 m of height, unless a dungeon of
        /// another type is nearer.
        /// </summary>
        public static int? InsideOf(Vec3 p, IReadOnlyList<Vec3> mine, IReadOnlyList<Vec3> others, float insideRadius)
        {
            if (p.Y < InteriorHeight) return null;
            int? best = null;
            float bestDistance = insideRadius;
            for (int i = 0; i < mine.Count; i++)
            {
                float d = Vec3.Flat(p, mine[i]);
                if (d < bestDistance && Math.Abs(p.Y - mine[i].Y) < 500f)
                {
                    best = i;
                    bestDistance = d;
                }
            }
            if (best == null) return null;
            foreach (var o in others)
                if (o.Y > InteriorHeight && Vec3.Flat(p, o) < bestDistance) return null;
            return best;
        }

        /// <summary>The entrance a point on the surface stands at: the nearest within <paramref name="radius"/>, flat.</summary>
        public static int? EntranceOf(Vec3 p, IReadOnlyList<Vec3> entrances, float radius)
        {
            if (p.Y >= InteriorHeight) return null;
            int? best = null;
            float bestDistance = radius;
            for (int i = 0; i < entrances.Count; i++)
            {
                float d = Vec3.Flat(p, entrances[i]);
                if (d < bestDistance)
                {
                    best = i;
                    bestDistance = d;
                }
            }
            return best;
        }

        public static int? NearestDoor(Vec3 generator, IReadOnlyList<Vec3> doors)
        {
            int? best = null;
            float bestDistance = DoorSearch;
            for (int i = 0; i < doors.Count; i++)
            {
                float d = Vec3.Flat(generator, doors[i]);
                if (d < bestDistance)
                {
                    best = i;
                    bestDistance = d;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// Which dungeon each player was last told about and where (1 entrance, 2 inside), so a trip gets one notice at the
    /// door and one more on going in; coming back out is not told again. Forget a player when they leave the area.
    /// </summary>
    public sealed class WelcomeMemory
    {
        private readonly Dictionary<long, (string Site, int Stage)> told = new Dictionary<long, (string, int)>();

        /// <summary>Records the player at this site and stage; true when they should be told.</summary>
        public bool Enter(long player, string site, int stage)
        {
            if (told.TryGetValue(player, out var was) && was.Site == site && stage <= was.Stage) return false;
            told[player] = (site, stage);
            return true;
        }

        public string? SiteOf(long player) => told.TryGetValue(player, out var was) ? was.Site : null;

        public void Leave(long player) => told.Remove(player);

        public IEnumerable<long> Players => told.Keys;
    }

    public readonly struct PlacedRoom
    {
        public PlacedRoom(int hash, Vec3 position, Vec3 euler)
        {
            Hash = hash;
            Position = position;
            Euler = euler;
        }

        public int Hash { get; }
        public Vec3 Position { get; }

        /// <summary>The room's rotation as Euler degrees (the game writes rotations as their euler angles).</summary>
        public Vec3 Euler { get; }
    }

    /// <summary>A generator's saved rooms (DungeonGenerator.Save, ZDOVars.s_roomData): int count, then per room int hash, position, euler angles.</summary>
    public static class RoomData
    {
        public static IReadOnlyList<PlacedRoom> Parse(byte[]? data)
        {
            var rooms = new List<PlacedRoom>();
            if (data == null || data.Length < 4) return rooms;
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                int n = r.ReadInt32();
                if (n < 0 || data.Length < 4 + (long)n * 28) return new List<PlacedRoom>();
                for (int i = 0; i < n; i++)
                    rooms.Add(new PlacedRoom(r.ReadInt32(), new Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), new Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle())));
            }
            return rooms;
        }

        public static PlacedRoom? Find(IReadOnlyList<PlacedRoom> rooms, int hash)
        {
            foreach (var room in rooms)
                if (room.Hash == hash) return room;
            return null;
        }
    }

    public enum DungeonOutcome
    {
        /// <summary>Never seen before: the clock starts now.</summary>
        FirstSight,
        /// <summary>A boss dungeon whose boss lives: never touched.</summary>
        BossAlive,
        /// <summary>A boss dungeon whose boss room was not found: it cannot be reset.</summary>
        NoBossPlace,
        Waiting,
        NothingTaken,
        /// <summary>A player is inside or holds one of its objects, its generator or its door.</summary>
        Occupied,
        Regenerate,
    }

    public static class DungeonRegrowRules
    {
        public static DungeonOutcome Decide(Stamp? lastVisit, Stamp now, ClockKind clock, double delay, bool regenerateNow, bool bossKind,
            bool bossAlive, bool bossPlaceKnown, int missing, bool occupied)
        {
            if (!lastVisit.HasValue && !regenerateNow) return DungeonOutcome.FirstSight;
            if (bossKind && bossAlive) return DungeonOutcome.BossAlive;
            if (bossKind && !bossPlaceKnown) return DungeonOutcome.NoBossPlace;
            if (!regenerateNow && !ClockRules.Due(clock, delay, lastVisit!.Value.CompletedWith(now), now)) return DungeonOutcome.Waiting;
            if (missing == 0 && !bossKind) return DungeonOutcome.NothingTaken;
            if (occupied) return DungeonOutcome.Occupied;
            return DungeonOutcome.Regenerate;
        }

        /// <summary>
        /// A remembered dungeon spot may come back: nothing a player built stands within <paramref name="clearance"/>
        /// metres of it (flat, within the shared height window); 0 turns the check off (Dungeon Resources / Resource
        /// Build Clearance, user decision R-Q2).
        /// </summary>
        public static bool SpotClear(Vec3 spot, float clearance, IEnumerable<Vec3> playerBuilt) =>
            !WorldRules.BuiltNear(spot, clearance, playerBuilt);

        /// <summary>Whole units for a notice: inside, the visit has just restarted the clock (full delay); at the entrance it is still running.</summary>
        public static int UnitsLeft(Stamp? lastVisit, Stamp now, ClockKind clock, double delay, bool inside)
        {
            if (inside || !lastVisit.HasValue) return ClockRules.WholeUnitsLeft(ClockRules.Clamp(clock, delay));
            return ClockRules.WholeUnitsLeft(ClockRules.Remaining(clock, delay, lastVisit.Value.CompletedWith(now), now));
        }
    }
}
