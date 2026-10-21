// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from EliteCreaturesReborn 3.20.0, Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later (ValheimMods monorepo
// at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: EliteCreaturesReborn/EliteCreaturesReborn/Breeding/Lineage.cs
// Begin/Take/Notice/End, Patches/BreedingPatch.cs GrowUpPatch.GrowsNow). Full notice in
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
//
// Changed by expDARE, 2026: Unity-free generic slot keyed by the offspring prefab name (a stray object of another
// prefab cannot claim); Shepherd lineage records instead of ECR traits; hatch reads the Shepherd egg record.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Shepherd.Breeding
{
    /// <summary>
    /// The one-call newborn claim (design 10.4): a Procreate, Growup or EggGrow prefix arms it with the record and the
    /// prefab it expects; the first object of that prefab to wake inside the game's <c>Instantiate</c> claims it; the
    /// finalizer clears it, so nothing leaks to a later spawn.
    /// </summary>
    public sealed class ClaimSlot<T> where T : class
    {
        private T? record;
        private string? prefab;

        public bool Armed => record != null;

        public void Arm(T value, string offspringPrefab)
        {
            record = value ?? throw new ArgumentNullException(nameof(value));
            prefab = offspringPrefab ?? throw new ArgumentNullException(nameof(offspringPrefab));
        }

        /// <summary>Whether an object of this prefab would claim the armed record (no state change).</summary>
        public bool Claims(string? objectPrefab) => record != null && objectPrefab != null && objectPrefab == prefab;

        /// <summary>The armed record, once, for an object of the expected prefab; otherwise null.</summary>
        public T? Claim(string? objectPrefab)
        {
            if (!Claims(objectPrefab)) return null;
            T? claimed = record;
            Clear();
            return claimed;
        }

        public void Clear()
        {
            record = null;
            prefab = null;
        }
    }

    /// <summary>Records carried through growing up and hatching (design 10.6, C7).</summary>
    public static class Carry
    {
        /// <summary>The Growup tick that will grow the young up (the game's own test, which it makes next).</summary>
        public static bool GrowsNow(double secondsSinceSpawn, double growTime) => secondsSinceSpawn > growTime;

        /// <summary>The adult keeps the young's record and token.</summary>
        public static LineageRecord GrowUp(LineageRecord young) => Lineage.GrowUp(young);

        /// <summary>The hatchling's record from the egg item's <c>shepherd.egg</c> text; null for a plain or unreadable egg.</summary>
        public static LineageRecord? Hatch(string? eggText, string token) =>
            EggCodec.TryDecode(eggText, out EggRecord? egg) && egg != null ? Lineage.Hatch(egg, token) : null;
    }
}
