// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/Regrow.cs IsWhole, Zone, ZoneNear and the per-spot branch of Sweep). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: pure functions over facts; the near, held and build tests go through Delve's shared guards
// (a held object counts as near, as in RestockRegen); Decide counts whole in-game days as RestockRegen does, DecideAt
// counts on a Delve two-clock stamp (in-game days or real hours, design B1).
using System;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Regrow
{
    /// <summary>What stands at a remembered spot now.</summary>
    public enum SpotState { Whole, Damaged, Missing }

    public enum SpotOutcome { Whole, Waiting, Near, Built, Restore, Replace }

    public static class RegrowRules
    {
        /// <summary>The game's zone edge in metres.</summary>
        public const float ZoneSize = 64f;

        /// <summary>
        /// Whole: the whole prefab with no damage recorded: no MineRock5 health string, not marked dead (a flametal spire
        /// that is leaving), and for a drifting kind no MineRock chunk hit (each hit chunk leaves a "Health&lt;n&gt;" float).
        /// </summary>
        public static bool IsWhole(bool wholePrefab, string? healthText, bool dead, bool anyChunkHit, bool drifts) =>
            wholePrefab && string.IsNullOrEmpty(healthText) && !dead && !(drifts && anyChunkHit);

        /// <summary>
        /// One remembered spot at a sweep: a whole object stays; otherwise the spot waits until its zone has gone the
        /// kind's days without a visit (regenerate now skips that), then waits while a player is near or holds the damaged
        /// object, or while something a player built stands within the clearance; else a missing object is restored and a
        /// damaged one replaced.
        /// </summary>
        public static SpotOutcome Decide(SpotState state, int lastVisitDay, int today, int days, bool regenerateNow, GuardInput guard)
        {
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            if (state == SpotState.Whole) return SpotOutcome.Whole;
            if (!regenerateNow && today - lastVisitDay < Math.Max(1, days)) return SpotOutcome.Waiting;
            var g = Guards.Evaluate(new GuardInput(guard.Center, guard.PlayerRadius)
            {
                Interior = guard.Interior,
                Players = guard.Players,
                HeldByPlayer = guard.HeldByPlayer && state == SpotState.Damaged,
                Clearance = guard.Clearance,
                Built = guard.Built,
                BaseGuard = false,
            });
            if (g == GuardResult.PlayerNear || g == GuardResult.Held) return SpotOutcome.Near;
            if (g == GuardResult.Built) return SpotOutcome.Built;
            return state == SpotState.Damaged ? SpotOutcome.Replace : SpotOutcome.Restore;
        }

        /// <summary>
        /// <see cref="Decide"/> on the kind's clock: the zone's last visit is a two-clock stamp (unknown: the clock starts
        /// at this evaluation, so the spot waits) and the delay is fractional.
        /// </summary>
        public static SpotOutcome DecideAt(SpotState state, Stamp? lastVisit, Stamp now, ClockKind clock, double delay, bool regenerateNow, GuardInput guard)
        {
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            if (state == SpotState.Whole) return SpotOutcome.Whole;
            bool due = regenerateNow || (lastVisit.HasValue && ClockRules.Due(clock, delay, lastVisit.Value.CompletedWith(now), now));
            if (!due) return SpotOutcome.Waiting;
            return Decide(state, 0, 1, 1, true, guard);
        }

        /// <summary>Some point of the zone's 64 m square lies within <paramref name="radius"/> of <paramref name="p"/> (flat).</summary>
        public static bool ZoneNear((int X, int Z) zone, Vec3 p, float radius)
        {
            float half = ZoneSize / 2f;
            float dx = Math.Max(0f, Math.Abs(p.X - zone.X * ZoneSize) - half);
            float dz = Math.Max(0f, Math.Abs(p.Z - zone.Z * ZoneSize) - half);
            return dx * dx + dz * dz <= radius * radius;
        }

        /// <summary>The zone of a position (64 m squares centred on multiples of 64), as the visit record keys it.</summary>
        public static (int X, int Z) Zone(Vec3 p) =>
            ((int)Math.Floor((p.X + ZoneSize / 2f) / ZoneSize), (int)Math.Floor((p.Z + ZoneSize / 2f) / ZoneSize));
    }
}
