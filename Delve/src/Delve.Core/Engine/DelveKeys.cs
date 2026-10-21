// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the ZDO keys Delve writes (design: world state in keys delve.*) and how a two-clock stamp is
// stored on an object: the world day plus one as a float (0 = none) and the UTC time as ticks (0 = none).
using System;
using System.Collections.Generic;

namespace ExtraPotions.Delve.Engine
{
    public static class DelveKeys
    {
        public const string ChestSinceDay = "delve.chest.since_day";
        public const string ChestSinceUtc = "delve.chest.since_utc";
        public const string ChestFirstBy = "delve.chest.first_by";
        public const string ChestFirstDay = "delve.chest.first_day";
        public const string ChestLastBy = "delve.chest.last_by";
        public const string ChestLastDay = "delve.chest.last_day";

        /// <summary>The fixed keys; per-kind keys come from <see cref="Spots"/>, <see cref="VisitDay"/> and <see cref="VisitUtc"/>.</summary>
        public static IReadOnlyList<string> Fixed { get; } = Array.AsReadOnly(new[]
        {
            ChestSinceDay, ChestSinceUtc, ChestFirstBy, ChestFirstDay, ChestLastBy, ChestLastDay,
        });

        private static string Stem(DelveKind kind) => DelveCore.ZdoPrefix + kind.ToString().ToLowerInvariant();

        /// <summary>A dungeon kind's remembered spots on its generator (the DungeonSpots package).</summary>
        public static string Spots(DelveKind kind) => Stem(kind) + ".spots";

        /// <summary>A site's last visit (or reset) on its generator: world day plus one.</summary>
        public static string VisitDay(DelveKind kind) => Stem(kind) + ".visit_day";

        /// <summary>A site's last visit (or reset) on its generator: UTC ticks.</summary>
        public static string VisitUtc(DelveKind kind) => Stem(kind) + ".visit_utc";

        public static float EncodeDay(Stamp? stamp) => stamp.HasValue && stamp.Value.HasDay ? (float)(stamp.Value.Day + 1) : 0f;

        public static long EncodeUtc(Stamp? stamp) => stamp.HasValue && stamp.Value.HasUtc ? stamp.Value.Utc.Ticks : 0L;

        /// <summary>The stored stamp, or null when neither part is set.</summary>
        public static Stamp? Decode(float dayPlusOne, long utcTicks)
        {
            bool hasDay = dayPlusOne > 0f && !float.IsNaN(dayPlusOne) && !float.IsInfinity(dayPlusOne);
            bool hasUtc = utcTicks > 0L && utcTicks <= DateTime.MaxValue.Ticks;
            if (hasDay && hasUtc) return Stamp.At(dayPlusOne - 1.0, new DateTime(utcTicks, DateTimeKind.Utc));
            if (hasDay) return Stamp.DayOnly(dayPlusOne - 1.0);
            if (hasUtc) return Stamp.UtcOnly(new DateTime(utcTicks, DateTimeKind.Utc));
            return null;
        }
    }
}
