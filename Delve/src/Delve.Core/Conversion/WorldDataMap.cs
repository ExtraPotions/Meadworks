// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: where RestockRegen's world data goes in Delve (design 10). RestockRegen's key names are read from
// its MIT source (src/Restocker.cs, src/History.cs, src/RestockRegenPlugin.cs) and the kind catalog.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Regrow;

namespace ExtraPotions.Delve.Conversion
{
    /// <summary>How a RestockRegen value is stored.</summary>
    public enum WorldValue
    {
        /// <summary>An int in-game day + 1 (0 = never); Delve stores the day as its stamp's day component.</summary>
        DayPlusOne,
        /// <summary>A player's character name.</summary>
        Text,
        /// <summary>A spot package (version 1, count, prefab, position, rotation), copied as is.</summary>
        SpotPackage,
    }

    public sealed class WorldKeyMap
    {
        internal WorldKeyMap(string restockRegenKey, string delveKey, WorldValue value)
        {
            RestockRegenKey = restockRegenKey;
            DelveKey = delveKey;
            Value = value;
        }

        public string RestockRegenKey { get; }
        public string DelveKey { get; }
        public WorldValue Value { get; }
    }

    /// <summary>
    /// RestockRegen ZDO keys and their Delve keys. The converter copies a value only when the object has no Delve value
    /// yet, and leaves RestockRegen's keys in place. The world-object sidecar <c>worlds_local/&lt;world&gt;.restockregen.txt</c>
    /// is read with <see cref="RecordFile"/>. Every Delve key is the runtime's own key from <see cref="DelveKeys"/>.
    /// </summary>
    public static class WorldDataMap
    {
        public const string ChestStampDay = DelveKeys.ChestSinceDay;
        public const string ChestStampUtc = DelveKeys.ChestSinceUtc;
        public const string ChestFirstBy = DelveKeys.ChestFirstBy;
        public const string ChestFirstDay = DelveKeys.ChestFirstDay;
        public const string ChestLastBy = DelveKeys.ChestLastBy;
        public const string ChestLastDay = DelveKeys.ChestLastDay;

        private static readonly Lazy<WorldKeyMap[]> all = new Lazy<WorldKeyMap[]>(() =>
            new[]
            {
                new WorldKeyMap("restockregen_emptysince", ChestStampDay, WorldValue.DayPlusOne),
                new WorldKeyMap("restockregen_firstby", ChestFirstBy, WorldValue.Text),
                new WorldKeyMap("restockregen_firstday", ChestFirstDay, WorldValue.DayPlusOne),
                new WorldKeyMap("restockregen_lastby", ChestLastBy, WorldValue.Text),
                new WorldKeyMap("restockregen_lastday", ChestLastDay, WorldValue.DayPlusOne),
            }
            .Concat(KindCatalog.All.Where(k => k.RestockRegenSpotsKey != null).SelectMany(k => new[]
            {
                new WorldKeyMap(k.RestockRegenSpotsKey!, SpotsKey(k), WorldValue.SpotPackage),
                new WorldKeyMap(k.RestockRegenVisitKey!, VisitKey(k), WorldValue.DayPlusOne),
            }))
            .ToArray());

        public static IReadOnlyList<WorldKeyMap> All => all.Value;

        public static WorldKeyMap? For(string restockRegenKey) => all.Value.FirstOrDefault(m => m.RestockRegenKey == restockRegenKey);

        /// <summary>The generator ZDO key of a dungeon kind's remembered spots (<see cref="DelveKeys.Spots"/>).</summary>
        public static string SpotsKey(KindInfo kind) => DelveKeys.Spots(kind.Kind);

        /// <summary>The generator ZDO key of a dungeon kind's last visit day (<see cref="DelveKeys.VisitDay"/>).</summary>
        public static string VisitKey(KindInfo kind) => DelveKeys.VisitDay(kind.Kind);
    }
}
