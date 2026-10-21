// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackCompiledSnapshot.cs. Source copyright and licence retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, catalog and diagnostics seams; Unity rotation value replaced by a portable
// RotationOffset; world-applied snapshot omitted; generated clone names use Edge. Normalisation rules retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ExtraPotions.Edge.Secondaries.Adopted;

internal sealed class SecondaryAttackCompiledSnapshot
{
    public static readonly SecondaryAttackCompiledSnapshot Empty = new(
        0,
        new Dictionary<string, NormalizedWeaponConfig>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, NormalizedWeaponConfig>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, NormalizedWeaponConfig>(StringComparer.OrdinalIgnoreCase),
        null,
        new Dictionary<string, NormalizedMagicSummonOverrideConfig>(StringComparer.OrdinalIgnoreCase));

    public SecondaryAttackCompiledSnapshot(
        int snapshotId,
        IReadOnlyDictionary<string, NormalizedWeaponConfig> weapons,
        IReadOnlyDictionary<string, NormalizedWeaponConfig> globalRangedPresets,
        IReadOnlyDictionary<string, NormalizedWeaponConfig> globalBloodMagicPresets,
        NormalizedWeaponConfig? globalMeleeFallback,
        IReadOnlyDictionary<string, NormalizedMagicSummonOverrideConfig> magicSummons)
    {
        SnapshotId = snapshotId;
        Weapons = CopyAsReadOnly(weapons);
        GlobalRangedPresets = CopyAsReadOnly(globalRangedPresets);
        GlobalBloodMagicPresets = CopyAsReadOnly(globalBloodMagicPresets);
        GlobalMeleeFallback = globalMeleeFallback?.Clone();
        MagicSummons = CopyAsReadOnly(magicSummons);
    }

    public int SnapshotId { get; }

    public IReadOnlyDictionary<string, NormalizedWeaponConfig> Weapons { get; }

    public IReadOnlyDictionary<string, NormalizedWeaponConfig> GlobalRangedPresets { get; }

    public IReadOnlyDictionary<string, NormalizedWeaponConfig> GlobalBloodMagicPresets { get; }

    public NormalizedWeaponConfig? GlobalMeleeFallback { get; }

    public IReadOnlyDictionary<string, NormalizedMagicSummonOverrideConfig> MagicSummons { get; }

    private static IReadOnlyDictionary<string, TValue> CopyAsReadOnly<TValue>(
        IReadOnlyDictionary<string, TValue> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Dictionary<string, TValue> copy = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, TValue value) in source)
        {
            copy[key] = value;
        }

        return new ReadOnlyDictionary<string, TValue>(copy);
    }
}

