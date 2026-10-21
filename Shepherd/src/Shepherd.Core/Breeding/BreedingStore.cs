// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>
    /// Typed key-value access to one animal's ZDO, so the codecs stay Unity-free. Missing keys read as the game's
    /// defaults: "", 0, 0, false. The plugin's adapter writes only on the ZDO owner.
    /// </summary>
    public interface IKeyStore
    {
        string GetString(string key);
        int GetInt(string key);
        long GetLong(string key);
        bool GetBool(string key);
        void Set(string key, string value);
        void Set(string key, int value);
        void Set(string key, long value);
        void Set(string key, bool value);
    }

    /// <summary>Animal ZDO keys (design 10.1) and the egg custom-data key.</summary>
    public static class BreedingKeys
    {
        public const string Instance = "shepherd.instance";
        public const string Mother = "shepherd.mother";
        public const string Sire = "shepherd.sire";
        public const string MotherName = "shepherd.mother_name";
        public const string SireName = "shepherd.sire_name";
        public const string Generation = "shepherd.gen";
        public const string Born = "shepherd.born";
        public const string Breeder = "shepherd.breeder";
        public const string PendingSire = "shepherd.pending_sire";
        public const string PendingSireName = "shepherd.pending_sire_name";
        public const string PendingSireLevel = "shepherd.pending_sire_level";
        public const string PendingSireGeneration = "shepherd.pending_sire_gen";
        public const string PendingSireTraits = "shepherd.pending_sire_traits";
        /// <summary>World time in long milliseconds (the design's double; a ZDO has no double).</summary>
        public const string TwinDue = "shepherd.twin_due";
        public const string IsTwin = "shepherd.is_twin";
        /// <summary>World time in long milliseconds.</summary>
        public const string ContentUntil = "shepherd.content_until";
        /// <summary>World time in long milliseconds.</summary>
        public const string ProduceNext = "shepherd.produce_next";
        public const string Traits = "shepherd.traits";
        /// <summary>Births so far (int), counted on the parent's owner; keepers' clients read it for Birth Experience.</summary>
        public const string Births = "shepherd.births";
        /// <summary>Egg <c>ItemData.m_customData</c> key holding <see cref="EggCodec"/> text.</summary>
        public const string Egg = "shepherd.egg";

        private static readonly string[] Keys =
        {
            Instance, Mother, Sire, MotherName, SireName, Generation, Born, Breeder, PendingSire, PendingSireName,
            PendingSireLevel, PendingSireGeneration, PendingSireTraits, TwinDue, IsTwin, ContentUntil, ProduceNext, Traits, Births, Egg,
        };

        public static IReadOnlyList<string> All => Keys;
    }

    /// <summary>Codecs between the breeding records and an animal's ZDO keys.</summary>
    public static class BreedingStore
    {
        public static void WriteLineage(IKeyStore store, LineageRecord record)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (record == null) throw new ArgumentNullException(nameof(record));
            store.Set(BreedingKeys.Instance, record.Token);
            store.Set(BreedingKeys.Mother, record.MotherToken);
            store.Set(BreedingKeys.Sire, record.SireToken);
            store.Set(BreedingKeys.MotherName, record.MotherName);
            store.Set(BreedingKeys.SireName, record.SireName);
            store.Set(BreedingKeys.Generation, record.Generation);
            store.Set(BreedingKeys.Born, record.BornDay);
            store.Set(BreedingKeys.Breeder, record.Breeder);
        }

        /// <summary>The animal's lineage, or null when Shepherd has not tracked it yet (no instance token).</summary>
        public static LineageRecord? ReadLineage(IKeyStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            string token = store.GetString(BreedingKeys.Instance);
            if (string.IsNullOrEmpty(token)) return null;
            return new LineageRecord(token, store.GetString(BreedingKeys.Mother), store.GetString(BreedingKeys.Sire),
                store.GetString(BreedingKeys.MotherName), store.GetString(BreedingKeys.SireName),
                Math.Max(0, store.GetInt(BreedingKeys.Generation)), store.GetInt(BreedingKeys.Born), store.GetString(BreedingKeys.Breeder));
        }

        /// <summary>Writes the sire captured at conception, or clears every pending key for null.</summary>
        public static void WritePendingSire(IKeyStore store, SireRecord? sire)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            store.Set(BreedingKeys.PendingSire, sire?.Token ?? "");
            store.Set(BreedingKeys.PendingSireName, sire?.Name ?? "");
            store.Set(BreedingKeys.PendingSireLevel, sire?.Level ?? 0);
            store.Set(BreedingKeys.PendingSireGeneration, sire?.Generation ?? 0);
            store.Set(BreedingKeys.PendingSireTraits, sire?.Traits ?? "");
        }

        /// <summary>The pending sire, or null when none was captured (level 0). An imported sire may have no token.</summary>
        public static SireRecord? ReadPendingSire(IKeyStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            int level = store.GetInt(BreedingKeys.PendingSireLevel);
            if (level < 1) return null;
            return new SireRecord(store.GetString(BreedingKeys.PendingSire), store.GetString(BreedingKeys.PendingSireName), level,
                store.GetInt(BreedingKeys.PendingSireGeneration), store.GetString(BreedingKeys.PendingSireTraits));
        }

        public static void SetTwinDue(IKeyStore store, double worldSeconds) => SetTime(store, BreedingKeys.TwinDue, worldSeconds);
        public static double GetTwinDue(IKeyStore store) => GetTime(store, BreedingKeys.TwinDue);
        public static void SetContentUntil(IKeyStore store, double worldSeconds) => SetTime(store, BreedingKeys.ContentUntil, worldSeconds);
        public static double GetContentUntil(IKeyStore store) => GetTime(store, BreedingKeys.ContentUntil);
        public static void SetProduceNext(IKeyStore store, double worldSeconds) => SetTime(store, BreedingKeys.ProduceNext, worldSeconds);
        public static double GetProduceNext(IKeyStore store) => GetTime(store, BreedingKeys.ProduceNext);

        public static bool IsTwin(IKeyStore store) => (store ?? throw new ArgumentNullException(nameof(store))).GetBool(BreedingKeys.IsTwin);
        public static void SetIsTwin(IKeyStore store, bool value) => (store ?? throw new ArgumentNullException(nameof(store))).Set(BreedingKeys.IsTwin, value);

        public static string GetTraits(IKeyStore store) => (store ?? throw new ArgumentNullException(nameof(store))).GetString(BreedingKeys.Traits);
        public static void SetTraits(IKeyStore store, string traits) => (store ?? throw new ArgumentNullException(nameof(store))).Set(BreedingKeys.Traits, traits ?? "");

        /// <summary>World seconds as long milliseconds; NaN, infinities and negatives store 0 (none).</summary>
        private static void SetTime(IKeyStore store, string key, double worldSeconds)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            bool valid = !double.IsNaN(worldSeconds) && !double.IsInfinity(worldSeconds) && worldSeconds > 0 && worldSeconds < long.MaxValue / 1000.0;
            store.Set(key, valid ? (long)Math.Round(worldSeconds * 1000.0) : 0L);
        }

        private static double GetTime(IKeyStore store, string key)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            return Math.Max(0L, store.GetLong(key)) / 1000.0;
        }
    }
}
