// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (Elite Creatures Reborn 3.20.0, ValheimMods at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: EliteCreaturesReborn/EliteCreaturesReborn/Rules/RespawnRules.cs MinutesFor and
// defaults, Patches/SpawnerRespawnPatch.cs SpawnerRespawn.Arm). The notice is in
// THIRD_PARTY_NOTICES/EliteCreaturesReborn-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: pure functions over the spawner's facts; the day length is passed in; Delve's settings carry
// the switches and days (Creature Respawn section). The server-side reset (ServerReset, Approach) is Meadworks-original:
// a server-only product cannot arm a spawner a client owns, so on a dedicated server Delve resets the cleared spawner's
// saved state for its owner to spawn again (to be confirmed by the DV15b native probe). PathFor, ArmMinutes and LastAlive
// (runtime path, timer and alive time on Delve's clocks) are Meadworks-original too.
using System;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Respawn
{
    /// <summary>
    /// How the world repopulates: camp spawners and dungeon spawners, each with its own timer in world days. Both are off
    /// by default: this can quietly undo a player's sense of progress if it runs too fast.
    /// </summary>
    public sealed class RespawnOptions
    {
        public RespawnOptions(bool camps, double campDays, bool dungeons, double dungeonDays)
        {
            Camps = camps;
            CampDays = campDays;
            Dungeons = dungeons;
            DungeonDays = dungeonDays;
        }

        /// <summary>Elite Creatures Reborn's defaults: both off, 5 and 7 days.</summary>
        public static RespawnOptions Defaults { get; } = new RespawnOptions(false, 5, false, 7);

        public bool Camps { get; }
        public double CampDays { get; }
        public bool Dungeons { get; }
        public double DungeonDays { get; }
    }

    public enum RespawnApproach
    {
        /// <summary>The authority owns the spawner: give the one-shot spawner a respawn timer, as Elite Creatures Reborn does.</summary>
        ArmLocal,
        /// <summary>A client owns it: reset its saved spawned state so its owner's game spawns it again.</summary>
        ServerReset,
    }

    public enum SpawnerReset { Leave, Waiting, Blocked, Reset }

    public static class SpawnerRules
    {
        /// <summary>Interiors are built far above the ground; the plugin asks the game (<c>Character.InInterior</c>), this is the portable stand-in.</summary>
        public const float InteriorHeight = 3000f;

        /// <summary>
        /// The spawner timer in the game's own unit, minutes of the world clock, or 0 when this kind of spawner is left
        /// alone. A day is <paramref name="daySeconds"/> / 60 of those minutes.
        /// </summary>
        public static double MinutesFor(bool inDungeon, RespawnOptions options, double daySeconds)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            double dayMinutes = daySeconds / 60.0;
            if (inDungeon) return options.Dungeons ? Math.Max(0, options.DungeonDays) * dayMinutes : 0;
            return options.Camps ? Math.Max(0, options.CampDays) * dayMinutes : 0;
        }

        /// <summary>
        /// The timer to give a spawner, or null to leave it. Only a spawner that has spawned before gets one: a fresh one
        /// keeps zero so the first population is the game's own (a timer counts from an unset last-alive time, which would
        /// hold the first spawn back). A spawner whose timer the game already set respawns as its designer intended.
        /// </summary>
        public static float? Arm(float currentMinutes, bool hasSpawned, double minutes)
        {
            if (currentMinutes > 0f || !hasSpawned) return null;
            return minutes > 0 ? (float)minutes : (float?)null;
        }

        public static RespawnApproach Approach(bool authorityOwnsSpawner) => authorityOwnsSpawner ? RespawnApproach.ArmLocal : RespawnApproach.ServerReset;

        /// <summary>
        /// The runtime path: the game's own timer (arming) only for a spawner the authority has loaded and owns, and only on
        /// the day clock, because the game's timer counts world minutes; every other spawner is reset through its ZDO.
        /// </summary>
        public static RespawnApproach PathFor(bool loadedAndOwned, ClockKind clock) =>
            loadedAndOwned && clock == ClockKind.InGameDays ? RespawnApproach.ArmLocal : RespawnApproach.ServerReset;

        /// <summary>A delay in world days as the game's spawner timer (world minutes), within the clock's range.</summary>
        public static double ArmMinutes(double days, double daySeconds) => ClockRules.Clamp(ClockKind.InGameDays, days) * daySeconds / 60.0;

        /// <summary>The spawner's last-alive time (ZDOVars.s_aliveTime, world ticks) as a stamp; unset is unknown.</summary>
        public static Stamp LastAlive(long aliveTicks, double daySeconds) => WorldClock.FromGameTicks(aliveTicks, daySeconds);

        public static bool InDungeon(Vec3 position) => position.Y > InteriorHeight;

        /// <summary>
        /// The server-side decision for a client-owned spawner: only a one-shot spawner that has spawned and whose creature
        /// is gone; due after the delay since its creature was last seen alive; held back by the guards (a player near).
        /// </summary>
        public static SpawnerReset ServerReset(bool oneShot, bool hasSpawned, bool creatureAlive, Stamp lastAlive, Stamp now,
            ClockKind clock, double delay, GuardInput guard) =>
            ServerReset(oneShot, hasSpawned, creatureAlive, lastAlive, now, clock, delay, guard, out _);

        /// <param name="blockedBy">Why a due spawner is <see cref="SpawnerReset.Blocked"/>: a player within the radius
        /// (<see cref="GuardResult.PlayerNear"/>), a client that owns and has it loaded (<see cref="GuardResult.Held"/>), a
        /// build or a base; <see cref="GuardResult.Clear"/> for every other decision.</param>
        public static SpawnerReset ServerReset(bool oneShot, bool hasSpawned, bool creatureAlive, Stamp lastAlive, Stamp now,
            ClockKind clock, double delay, GuardInput guard, out GuardResult blockedBy)
        {
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            blockedBy = GuardResult.Clear;
            if (!oneShot || !hasSpawned || creatureAlive) return SpawnerReset.Leave;
            if (!ClockRules.Due(clock, delay, lastAlive.CompletedWith(now), now)) return SpawnerReset.Waiting;
            blockedBy = Guards.Evaluate(guard);
            return blockedBy == GuardResult.Clear ? SpawnerReset.Reset : SpawnerReset.Blocked;
        }
    }
}
