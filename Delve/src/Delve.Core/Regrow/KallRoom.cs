// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Kall Fimbulbringer's return to the Deep North boss room. Facts read from the Valheim 1.0.20 game
// data (2026-10-19): the ZoneSystem location DN_Bossroom has no dungeon generator and no door; its interior, 5000 m above
// the location, holds a sleeping FrozenKing placed by the location itself at (-50, 5002, 200.48) facing 180 degrees, and
// an offering altar that summons another FrozenKing for three Hateful Blood. FrozenKing turns into FrozenKing_p2 and then
// FrozenKing_p3 during the fight. The game records the spawned location as a LocationProxy object keyed by its name.
using System;
using System.Collections.Generic;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Regrow
{
    public static class KallRoom
    {
        public const string Location = "DN_Bossroom";
        public const string Boss = "FrozenKing";
        public const string Proxy = "LocationProxy";

        /// <summary>Every form the boss takes during the fight; any of them alive in the room keeps the room untouched.</summary>
        public static readonly IReadOnlyList<string> Phases = Array.AsReadOnly(new[] { "FrozenKing", "FrozenKing_p2", "FrozenKing_p3" });

        /// <summary>The sleeping boss's place in the location prefab, relative to the location's own position and turn.</summary>
        public static readonly Vec3 BossOffset = new Vec3(-50f, 5002f, 200.48f);

        /// <summary>The boss's turn in the location prefab, in degrees, added to the location's turn.</summary>
        public const float BossYaw = 180f;

        /// <summary>Meters (flat) around the boss's place that are the room: its interior radius (32) plus a margin.</summary>
        public const float InsideRadius = 40f;

        /// <summary>Meters (flat) around the location at the surface where a player is at the entrance.</summary>
        public const float EntranceRadius = 30f;

        /// <summary>
        /// Where the location put the boss: the offset turned by the location's yaw (ZoneSystem turns locations about the
        /// vertical only) and added to the location's position, with the boss's own turn on top.
        /// </summary>
        public static (Vec3 Position, float Yaw) BossSpot(Vec3 location, float locationYaw)
        {
            double r = locationYaw * Math.PI / 180.0;
            float cos = (float)Math.Cos(r), sin = (float)Math.Sin(r);
            var offset = BossOffset;
            var position = new Vec3(location.X + offset.X * cos + offset.Z * sin, location.Y + offset.Y, location.Z - offset.X * sin + offset.Z * cos);
            float yaw = (locationYaw + BossYaw) % 360f;
            return (position, yaw < 0 ? yaw + 360f : yaw);
        }

        /// <summary>In the room: up in the interior and within <see cref="InsideRadius"/> (flat) of the boss's place.</summary>
        public static bool Inside(Vec3 p, Vec3 bossSpot) => p.Y >= DungeonSites.InteriorHeight && Vec3.Flat(p, bossSpot) <= InsideRadius;

        /// <summary>At the surface entrance: below the interiors and within <see cref="EntranceRadius"/> (flat) of the location.</summary>
        public static bool AtEntrance(Vec3 p, Vec3 location) => p.Y < DungeonSites.InteriorHeight && Vec3.Flat(p, location) <= EntranceRadius;

        /// <summary>
        /// The Queen's rule for one room: the clock starts at first sight; a living boss in any phase keeps the room
        /// untouched; otherwise once the delay without visitors has passed (or regenerate now) the boss returns, never
        /// while a player is in the room or holds its objects.
        /// </summary>
        public static DungeonOutcome Decide(Stamp? lastVisit, Stamp now, ClockKind clock, double delay, bool regenerateNow, bool bossAlive, bool occupied) =>
            DungeonRegrowRules.Decide(lastVisit, now, clock, delay, regenerateNow, true, bossAlive, true, 0, occupied);
    }
}
