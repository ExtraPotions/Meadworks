// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0
// Adapted CreatureModifierManager.cs Capture/RestoreModifierState,
// pinned at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473; original LICENSE retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: typed private-key migration and canonical Wildblood state boundaries.
// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Traits;

namespace ExtraPotions.Wildblood.AdoptedCm;

internal static partial class CreatureModifierManager
{
    private const string MissingCanonicalBlob = "\u0000";
    private const string PrivateStateImportedKey = "wildblood.cm.imported";
    private const ModifierMask CanonicalEngineMask = (ModifierMask)0xffffffffL & ~ModifierMask.Blink & ~ModifierMask.Corrosive;
    // Partial-class field order is unspecified. Resolve the source schema only after type initialization.
    private static readonly Lazy<Dictionary<int, int>> LegacyStateHashMap = new(() => ModifierStateKeys.ToDictionary(
        key => ("CreatureManager_" + key.Substring("wildblood.cm.".Length)).GetStableHashCode(),
        key => key.GetStableHashCode()));
    private static Dictionary<int, int> LegacyStateHashes => LegacyStateHashMap.Value;

    // The explicit source snapshot schema defines what is imported. Unrecognized/source keys are never removed.
    // The completion marker is written last, so a partial owner write can safely resume without replacing private fields.
    internal static bool MigrateOwnedState(ZDO zdo)
    {
        if (!TraitStore.CanUpdateModifiers(zdo)) return false;
        if (zdo.GetBool(PrivateStateImportedKey, false)) return true;
        bool privateApplied = zdo.GetBool(AppliedKey, false);
        bool legacyApplied = zdo.GetBool("CreatureManager_ModifiersApplied", false);
        if (privateApplied || legacyApplied)
        {
            long raw = privateApplied ? zdo.GetLong(Mask64Key, 0L) : zdo.GetLong("CreatureManager_ModifierMask64", 0L);
            // Existing inherited modifiers (including a valid plain birth blob) outrank an old source cache.
            if (!TryGetInheritedCanonicalMask(zdo, out var inherited))
            {
                if (!TryWriteCanonicalMask(zdo, (ModifierMask)raw)) return false;
            }
            else
            {
                int merged = TraitKeys.Int(zdo, TraitKeys.Source) == (int)ExtraPotions.Wildblood.Stars.SpawnSource.Breeding
                    ? 0 : LegacyMasks.FromCreatureManager(raw).Mutations;
                if (!TraitStore.TryUpdateModifiers(zdo, LegacyMasks.FromCreatureManager((long)inherited).Modifiers, merged)) return false;
            }
        }
        return MigrateOwnedKeys(zdo, ModifierStateKeys, PrivateStateImportedKey);
    }

    // Karma supplies its separate explicit identity schema; those fields never enter modifier/egg snapshots.
    internal static bool MigrateOwnedKeys(ZDO zdo, IEnumerable<string> privateKeys, string completionKey)
    {
        if (zdo == null || !zdo.IsOwner()) return false;
        if (zdo.GetBool(completionKey, false)) return true;
        var map = privateKeys.ToDictionary(
            key => ("CreatureManager_" + key.Substring("wildblood.cm.".Length)).GetStableHashCode(),
            key => key.GetStableHashCode());
        ZDOExtraData.GetData(zdo.m_uid, out var floats, out var vectors, out var quaternions,
            out var ints, out var longs, out var strings, out var byteArrays, out _);
        var existing = floats.Select(v => v.Key).Concat(vectors.Select(v => v.Key))
            .Concat(quaternions.Select(v => v.Key)).Concat(ints.Select(v => v.Key))
            .Concat(longs.Select(v => v.Key)).Concat(strings.Select(v => v.Key))
            .Concat(byteArrays.Select(v => v.Key)).ToHashSet();
        CopyLegacyEntries(floats, existing, map, (hash, value) => zdo.Set(hash, value));
        CopyLegacyEntries(vectors, existing, map, (hash, value) => zdo.Set(hash, value));
        CopyLegacyEntries(quaternions, existing, map, (hash, value) => zdo.Set(hash, value));
        CopyLegacyEntries(ints, existing, map, (hash, value) => zdo.Set(hash, value));
        CopyLegacyEntries(longs, existing, map, (hash, value) => zdo.Set(hash, value));
        CopyLegacyEntries(strings, existing, map, (hash, value) => zdo.Set(hash, value));
        CopyLegacyEntries(byteArrays, existing, map, (hash, value) => zdo.Set(hash, value.ToArray()));
        zdo.Set(completionKey, true);
        return true;
    }

    private static void CopyLegacyEntries<T>(IEnumerable<KeyValuePair<int, T>> source, HashSet<int> existing,
        IReadOnlyDictionary<int, int> map, Action<int, T> write)
    {
        foreach (var entry in source.Where(entry => map.ContainsKey(entry.Key)).ToArray())
        {
            int target = map[entry.Key];
            if (existing.Add(target)) write(target, entry.Value);
        }
    }

    /// <summary>Runs source runtime application after the single canonical roll and ECR baseline, without rolling again.</summary>
    internal static void ApplyStoredRuntimeState(Character character)
    {
        if (TryGetZdo(character, out ZDO zdo) && zdo.GetBool(AppliedKey, false)) ApplyRuntimeModifierStats(character, zdo);
    }

    private static bool TryGetInheritedCanonicalMask(ZDO zdo, out ModifierMask mask)
    {
        mask = ModifierMask.None;
        string blob = zdo.GetString(TraitKeys.Blob, MissingCanonicalBlob);
        if (blob == MissingCanonicalBlob || !TraitBlob.TryDecode(blob, out var traits)) return false;
        if (traits.Modifiers.Count == 0 && TraitKeys.Int(zdo, TraitKeys.Source) !=
            (int)ExtraPotions.Wildblood.Stars.SpawnSource.Breeding) return false;
        mask = (ModifierMask)LegacyMasks.ToCreatureManagerModifiers(traits.Modifiers);
        return true;
    }

    /// <summary>The modifiers a creature wearing these mutations may not carry (TraitOverlaps).</summary>
    private static ModifierMask OverlappedModifiers(int mutations)
    {
        ModifierMask excluded = ModifierMask.None;
        if (mutations == 0) return excluded;
        foreach (ModifierSpec spec in ModifierSpecs)
            foreach (Modifier modifier in LegacyMasks.FromCreatureManager((long)spec.Mask).Modifiers)
                if (TraitOverlaps.Excludes(mutations, modifier)) excluded |= spec.Mask;
        return excluded;
    }

    private static ModifierMask OverlappedModifiers(ZDO zdo) => OverlappedModifiers(TraitStore.ReadSet(zdo).Mutations);

    /// <summary>
    /// Load-time cleanup for a creature saved with both halves of a pair: the mutation stays and the modifier bit is
    /// cleared through the canonical writer, which leaves unreadable or future trait blobs untouched.
    /// </summary>
    private static void DropSavedOverlaps(Character character, ZDO zdo)
    {
        ModifierMask stored = GetStoredModifierMask(zdo);
        ModifierMask overlap = stored & OverlappedModifiers(zdo);
        if (overlap == ModifierMask.None || !SetStoredModifierMask(zdo, stored & ~overlap)) return;
        InvalidateModifierCaches(character);
        RefreshModifierHotPathState(character, zdo);
        InvalidateModifierHudState(character);
    }

    private static bool TryWriteCanonicalMask(ZDO zdo, ModifierMask mask)
    {
        var mapped = LegacyMasks.FromCreatureManager((long)mask, TraitStore.ReadSet(zdo).Biome);
        return TraitStore.TryUpdateModifiers(zdo, mapped.Modifiers, mapped.Mutations);
    }
}
