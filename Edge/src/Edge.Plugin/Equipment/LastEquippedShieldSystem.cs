// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// LastEquippedShieldSystem.cs and Compat/HunterLegacyCompat.cs (shield identity, duplicate handling and offhand exclusion).
// Changed by expDARE, 2026: namespace, Crucible module gates, swim restoration guard and shared diagnostics.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Secondaries.Adopted;
using HarmonyLib;

namespace ExtraPotions.Edge.Runtime.Equipment;

internal static class LastEquippedShieldSystem
{
    private const string ItemIdentityKey = "SecondaryAttacks.LastShieldId";
    private const string PlayerSelectionKey = "SecondaryAttacks.LastShieldSelection";
    private const string SelectionVersionPrefix = "v1:";

    private static bool _initialized;
    private static bool _disabledAfterRuntimeError;
    private static string? _lastDuplicateWarningId;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _disabledAfterRuntimeError = false;
        _lastDuplicateWarningId = null;
    }

    internal static void Dispose()
    {
        _initialized = false;
        _disabledAfterRuntimeError = false;
        _lastDuplicateWarningId = null;
    }

    internal static void HandleEquip(
        Humanoid humanoid,
        ItemDrop.ItemData item,
        bool triggerEquipEffects,
        bool equipSucceeded)
    {
        if (!equipSucceeded || item?.m_shared == null || humanoid is not Player player)
        {
            return;
        }

        try
        {
            if (!IsFeatureAvailable(player))
            {
                return;
            }

            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Shield:
                    if (!IsOffhandCrossbow(item))
                    {
                        RememberShield(player, item);
                    }
                    break;
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                    TryEquipRememberedShield(player, item, triggerEquipEffects);
                    break;
            }
        }
        catch (Exception exception)
        {
            _disabledAfterRuntimeError = true;
            RuleDiagnostics.Warning(
                $"Last-shield auto equip was disabled for this session after a runtime error. {exception}");
        }
    }

    private static bool IsFeatureAvailable(Player player) => _initialized && !_disabledAfterRuntimeError &&
        player != null && player == Player.m_localPlayer && !EdgeSwimReequip.Restoring && EdgeModules.Active(EdgeModule.LastShield);

    private static void RememberShield(Player player, ItemDrop.ItemData shield)
    {
        Dictionary<string, string> itemData = shield.m_customData ??= new Dictionary<string, string>();
        string shieldId;
        if (!itemData.TryGetValue(ItemIdentityKey, out string storedId) ||
            !TryNormalizeShieldId(storedId, out shieldId) ||
            !IsUniqueInPlayerInventory(player, shield, shieldId))
        {
            do
            {
                shieldId = Guid.NewGuid().ToString("N");
            }
            while (!IsUniqueInPlayerInventory(player, shield, shieldId));

            itemData[ItemIdentityKey] = shieldId;
        }
        else if (!string.Equals(storedId, shieldId, StringComparison.Ordinal))
        {
            itemData[ItemIdentityKey] = shieldId;
        }

        player.m_customData ??= new Dictionary<string, string>();
        player.m_customData[PlayerSelectionKey] = SelectionVersionPrefix + shieldId;
        _lastDuplicateWarningId = null;
    }

    private static void TryEquipRememberedShield(
        Player player,
        ItemDrop.ItemData equippedWeapon,
        bool triggerEquipEffects)
    {
        if (!ReferenceEquals(player.RightItem, equippedWeapon) || player.LeftItem != null)
        {
            return;
        }

        ItemDrop.ItemData? rememberedShield = FindRememberedShield(player);
        if (rememberedShield != null)
        {
            player.EquipItem(rememberedShield, triggerEquipEffects);
        }
    }

    private static ItemDrop.ItemData? FindRememberedShield(Player player)
    {
        if (player.m_customData == null ||
            !player.m_customData.TryGetValue(PlayerSelectionKey, out string storedSelection) ||
            !storedSelection.StartsWith(SelectionVersionPrefix, StringComparison.Ordinal) ||
            !TryNormalizeShieldId(storedSelection.Substring(SelectionVersionPrefix.Length), out string selectedId))
        {
            return null;
        }

        ItemDrop.ItemData? match = null;
        foreach (ItemDrop.ItemData candidate in player.GetInventory().GetAllItems())
        {
            if (candidate?.m_shared?.m_itemType != ItemDrop.ItemData.ItemType.Shield ||
                IsOffhandCrossbow(candidate) ||
                candidate.m_customData == null ||
                !candidate.m_customData.TryGetValue(ItemIdentityKey, out string candidateId) ||
                !TryNormalizeShieldId(candidateId, out string normalizedCandidateId) ||
                !string.Equals(normalizedCandidateId, selectedId, StringComparison.Ordinal))
            {
                continue;
            }

            if (match != null)
            {
                WarnAboutDuplicateSelectionIfNeeded(selectedId);
                return null;
            }

            match = candidate;
        }

        return match;
    }

    private static bool IsUniqueInPlayerInventory(
        Player player,
        ItemDrop.ItemData selectedShield,
        string shieldId)
    {
        foreach (ItemDrop.ItemData candidate in player.GetInventory().GetAllItems())
        {
            if (ReferenceEquals(candidate, selectedShield) || candidate?.m_customData == null)
            {
                continue;
            }

            if (candidate.m_customData.TryGetValue(ItemIdentityKey, out string candidateId) &&
                TryNormalizeShieldId(candidateId, out string normalizedCandidateId) &&
                string.Equals(normalizedCandidateId, shieldId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryNormalizeShieldId(string value, out string shieldId)
    {
        if (Guid.TryParseExact(value, "N", out Guid parsed))
        {
            shieldId = parsed.ToString("N");
            return true;
        }

        shieldId = string.Empty;
        return false;
    }

    // Pinned HunterLegacyCompat.IsOffhandCrossbow and OffhandPrefabs subset. No third-party reflection is needed here.
    private static readonly HashSet<string> OffhandPrefabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "OffhandCrossbowBlackforest_DO", "OffhandCrossbowMountain_DO",
        "OffhandCrossbowMistlands_DO", "OffhandCrossbowAshlands_DO"
    };
    private static bool IsOffhandCrossbow(ItemDrop.ItemData item) =>
        Chainloader.PluginInfos.ContainsKey("Dreanegade.Hunter_Legacy") && item?.m_dropPrefab != null && OffhandPrefabs.Contains(item.m_dropPrefab.name);

    private static void WarnAboutDuplicateSelectionIfNeeded(string shieldId)
    {
        if (string.Equals(_lastDuplicateWarningId, shieldId, StringComparison.Ordinal))
        {
            return;
        }

        _lastDuplicateWarningId = shieldId;
        RuleDiagnostics.Warning(
            "The remembered shield identity exists on more than one inventory item. " +
            "Auto equip was skipped; equip the intended shield manually to assign it a new identity.");
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem), typeof(ItemDrop.ItemData), typeof(bool))]
internal static class HumanoidEquipItemLastShieldPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        Humanoid __instance,
        ItemDrop.ItemData item,
        bool triggerEquipEffects,
        bool __result)
    {
        LastEquippedShieldSystem.HandleEquip(__instance, item, triggerEquipEffects, __result);
    }
}
