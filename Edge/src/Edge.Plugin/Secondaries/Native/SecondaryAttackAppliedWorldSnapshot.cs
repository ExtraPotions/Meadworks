// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackCompiledSnapshot.cs (applied world snapshot). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace and existing snapshot boundaries; native algorithms retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries.Adopted;
internal sealed class SecondaryAttackAppliedWorldSnapshot
{
    public static readonly SecondaryAttackAppliedWorldSnapshot Empty =
        new(SecondaryAttackCompiledSnapshot.Empty, new Dictionary<string, SecondaryAttackDefinition>(StringComparer.OrdinalIgnoreCase), 0);

    public SecondaryAttackAppliedWorldSnapshot(
        SecondaryAttackCompiledSnapshot compiledSnapshot,
        IReadOnlyDictionary<string, SecondaryAttackDefinition> definitionsByPrefabName,
        int applyRevision)
    {
        CompiledSnapshot = compiledSnapshot ?? throw new ArgumentNullException(nameof(compiledSnapshot));
        DefinitionsByPrefabName = definitionsByPrefabName ?? throw new ArgumentNullException(nameof(definitionsByPrefabName));
        ApplyRevision = applyRevision;
    }

    public SecondaryAttackCompiledSnapshot CompiledSnapshot { get; }

    public int SnapshotId => CompiledSnapshot.SnapshotId;

    public int ApplyRevision { get; }

    public IReadOnlyDictionary<string, SecondaryAttackDefinition> DefinitionsByPrefabName { get; }

}
