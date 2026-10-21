// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the full dungeon reset (design C1, DV09) from MoreDungeonResets' documented behavior (README and
// config only, behavior reference): wipe a dungeon interior and generate it again with the current room counts. Guards
// are Delve's own: never with a player inside, a tombstone inside postpones it (user decision Q2 a), a player piece
// inside blocks it while Full Reset Build Clearance is on, never inside a player base.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Reset
{
    public enum FullResetOutcome { FirstSight, Waiting, PlayerInside, Tombstone, Built, PlayerBase, Reset }

    public enum WipeAction { Keep, Remove }

    public static class FullResetRules
    {
        /// <summary>Meters added around the generator's zone volume, so objects at the walls are inside.</summary>
        public const float Margin = 8f;

        /// <summary>
        /// The DV01b native probe (a server-side wipe and regeneration reaching a client correctly) is accepted: user decision
        /// 2026-10-10 12:11 takes run network-ffebf776 (2026-10-08) as the probe. Full resets run live; the Full Reset Dry Run
        /// and Dry Run All settings still turn them into reports.
        /// </summary>
        public const bool NativeProbePassed = true;

        public static bool EffectiveDryRun(bool dryRun) => dryRun || !NativeProbePassed;

        public static FullResetOutcome Decide(Stamp? lastVisit, Stamp now, ClockKind clock, double delay, bool regenerateNow,
            bool playerInside, bool tombstoneInside, bool builtInside, bool buildClearance, bool inPlayerBase)
        {
            if (!lastVisit.HasValue && !regenerateNow) return FullResetOutcome.FirstSight;
            if (!regenerateNow && !ClockRules.Due(clock, delay, lastVisit!.Value.CompletedWith(now), now)) return FullResetOutcome.Waiting;
            if (playerInside) return FullResetOutcome.PlayerInside;
            if (tombstoneInside) return FullResetOutcome.Tombstone;
            if (builtInside && buildClearance) return FullResetOutcome.Built;
            if (inPlayerBase) return FullResetOutcome.PlayerBase;
            return FullResetOutcome.Reset;
        }

        /// <summary>The Queen's citadel, the one dungeon type whose reset also concerns a boss and a sealed door at the surface.</summary>
        public const string Citadel = "DG_DvergrBoss";

        /// <summary>
        /// After a citadel is generated again: the Queen is placed in her room when the new rooms did not bring her and her
        /// room was found. The reset already waited for every guard, so nobody is inside.
        /// </summary>
        public static bool RestoreQueen(string generator, bool queenInside, bool bossRoomFound) =>
            generator == Citadel && !queenInside && bossRoomFound;

        /// <summary>After a citadel is generated again: its sealed door at the surface locks again while Close Sealed Door is on.</summary>
        public static bool RelockDoor(string generator, bool closeSealedDoor, bool doorFound) =>
            generator == Citadel && closeSealedDoor && doorFound;

        /// <summary>The generator stays (it is generated again in place), players and tombstones are never removed; everything else inside goes.</summary>
        public static WipeAction Wipe(bool isGenerator, bool isPlayer, bool isTombstone) =>
            isGenerator || isPlayer || isTombstone ? WipeAction.Keep : WipeAction.Remove;

        /// <summary>
        /// Generation either returns a replacement, or it does not. The old interior is removed only when the replacement
        /// exists. A failed attempt removes whatever that attempt created and leaves the old ids.
        /// </summary>
        public readonly struct ResetCommit
        {
            private ResetCommit(bool removeOld, bool removeCreated)
            {
                RemoveOld = removeOld;
                RemoveCreated = removeCreated;
            }

            public bool RemoveOld { get; }
            public bool RemoveCreated { get; }

            public static ResetCommit For(bool replacementReady) =>
                replacementReady ? new ResetCommit(true, false) : new ResetCommit(false, true);

            public static long[] Surviving(IReadOnlyList<long> oldIds, IReadOnlyList<long> createdIds, bool replacementReady)
            {
                if (oldIds == null) throw new ArgumentNullException(nameof(oldIds));
                if (createdIds == null) throw new ArgumentNullException(nameof(createdIds));
                var commit = For(replacementReady);
                return (commit.RemoveCreated ? oldIds : createdIds).ToArray();
            }
        }

        /// <summary>
        /// The interior volume: centered on the generator's zone on the ground plane (as DungeonGenerator.Generate centers its
        /// bounds) and on the generator's height, the generator's zone size plus <see cref="Margin"/> on every side.
        /// </summary>
        public static Box Interior(Vec3 generator, Vec3 zoneSize, Vec3 zoneCenter)
        {
            float hx = zoneSize.X / 2f + Margin, hy = zoneSize.Y / 2f + Margin, hz = zoneSize.Z / 2f + Margin;
            if (!(hx > 0) || !(hy > 0) || !(hz > 0)) throw new ArgumentException("Expected a positive zone size.", nameof(zoneSize));
            return new Box(new Vec3(zoneCenter.X - hx, generator.Y - hy, zoneCenter.Z - hz), new Vec3(zoneCenter.X + hx, generator.Y + hy, zoneCenter.Z + hz));
        }
    }
}
