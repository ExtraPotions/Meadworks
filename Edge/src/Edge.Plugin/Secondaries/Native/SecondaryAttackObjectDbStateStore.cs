// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackObjectDbStateStore.cs (native definition types and compilation; game algorithms retained). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, portable config/canonical catalog and diagnostics boundaries;
// portable rotation values converted at the native boundary. No ServerSync or source UI/config ownership.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;

internal static class SecondaryAttackObjectDbStateStore
{
    // Use the source compatibility store's SharedData ownership boundary: a
    // prefab can outlive its ObjectDB, whose Unity equality then reports null.
    private static readonly ConditionalWeakTable<ItemDrop.ItemData.SharedData, Attack> Snapshots = new();
    private static readonly List<WeakReference<ItemDrop.ItemData.SharedData>> Owned = new();

    public static void CaptureSecondaryAttack(
        ObjectDB objectDb,
        string prefabName,
        ItemDrop.ItemData.SharedData sharedData)
    {
        if (objectDb == null || string.IsNullOrWhiteSpace(prefabName) || sharedData == null ||
            Snapshots.TryGetValue(sharedData, out _))
        {
            return;
        }

        Snapshots.Add(sharedData, SecondaryAttackManager.CloneAttack(sharedData.m_secondaryAttack));
        Owned.Add(new WeakReference<ItemDrop.ItemData.SharedData>(sharedData));
    }

    private static void RestoreShared(ItemDrop.ItemData.SharedData sharedData)
    {
        if (!Snapshots.TryGetValue(sharedData, out Attack? original)) return;
        sharedData.m_secondaryAttack = SecondaryAttackManager.CloneAttack(original);
        Snapshots.Remove(sharedData);
    }

    public static void Restore(ObjectDB objectDb)
    {
        if (objectDb == null) return;
        foreach (GameObject itemPrefab in objectDb.m_items)
        {
            ItemDrop.ItemData.SharedData? sharedData = itemPrefab == null
                ? null : itemPrefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            if (sharedData != null) RestoreShared(sharedData);
        }
    }

    internal static void RestoreOwned()
    {
        // Restore captured references even after the old database was destroyed,
        // replaced its item list, or the item disappeared from the new registry.
        foreach (var reference in Owned)
            if (reference.TryGetTarget(out var sharedData)) RestoreShared(sharedData);
        Owned.Clear();
    }

    public static bool TryGetOriginalSecondaryAttack(ObjectDB objectDb, string prefabName, out Attack? attack)
    {
        attack = null;
        if (objectDb == null || string.IsNullOrWhiteSpace(prefabName)) return false;
        foreach (GameObject itemPrefab in objectDb.m_items)
        {
            if (itemPrefab == null || !string.Equals(itemPrefab.name, prefabName.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            var sharedData = itemPrefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            if (sharedData == null || !Snapshots.TryGetValue(sharedData, out Attack? original)) return false;
            attack = SecondaryAttackManager.CloneAttack(original);
            return true;
        }
        return false;
    }
}
