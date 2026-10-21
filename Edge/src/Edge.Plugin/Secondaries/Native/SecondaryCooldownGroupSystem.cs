// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryCooldownGroupSystem.cs and SecondaryCooldownHudSystem.cs (entry data subset). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespaces, canonical catalogs, Crucible settings and diagnostics;
// native source algorithms retained. Candidate stage, dispatch and prefab application are a separate unit.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Config;

internal enum NativeCooldownFillMode
    {
        Cooldown,
        Charge
    }

internal readonly struct NativeCooldownEntry(Sprite? icon, float remaining, float duration, string badge = "", NativeCooldownFillMode fillMode = NativeCooldownFillMode.Cooldown, string presetKey = "")
    {
        internal readonly string PresetKey = presetKey;
        internal readonly Sprite? Icon = icon;
        internal readonly float Remaining = Mathf.Max(0f, remaining);
        internal readonly float Duration = Mathf.Max(remaining, duration);
        internal readonly string Badge = badge ?? "";
        internal readonly NativeCooldownFillMode Mode = fillMode;
        internal readonly float Fraction = duration > 0.001f ? Mathf.Clamp01(remaining / duration) : 0f;
        internal readonly int SortOrder = fillMode == NativeCooldownFillMode.Charge ? 0 : 1;
    }

internal static class SecondaryCooldownGroupSystem
{
    private static readonly ConditionalWeakTable<Character, CharacterCooldownState> Cooldowns = new();

    internal static bool IsReady(Character attacker, string cooldownGroup)
    {
        if (attacker == null || string.IsNullOrWhiteSpace(cooldownGroup))
        {
            return true;
        }

        if (ShouldBypassCooldowns(attacker))
        {
            return true;
        }

        string key = cooldownGroup.Trim();
        CharacterCooldownState state = Cooldowns.GetValue(attacker, _ => new CharacterCooldownState());
        EnsureCurrentApplyRevision(state);
        return !TryGetActive(state, key, SecondaryAttackManager.GetNetworkTimeSeconds(), out _);
    }

    internal static bool TryConsume(
        Character attacker,
        string cooldownGroup,
        float duration,
        Sprite? icon, string presetKey = "")
    {
        if (attacker == null)
        {
            return true;
        }

        if (ShouldBypassCooldowns(attacker))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(cooldownGroup))
        {
            return true;
        }

        string key = cooldownGroup.Trim();
        CharacterCooldownState state = Cooldowns.GetValue(attacker, _ => new CharacterCooldownState());
        EnsureCurrentApplyRevision(state);
        double now = SecondaryAttackManager.GetNetworkTimeSeconds();
        if (TryGetActive(state, key, now, out _))
        {
            return false;
        }

        state.EntriesByGroup.Remove(key);
        float normalizedDuration = Mathf.Max(0f, duration);
        if (normalizedDuration <= 0f)
        {
            return true;
        }

        state.EntriesByGroup[key] = new CooldownEntry(
            now + normalizedDuration,
            normalizedDuration,
            icon, presetKey);
        return true;
    }

    internal static bool IsCooldownActive(
        Character attacker,
        string cooldownGroup,
        out float remaining)
    {
        remaining = 0f;
        if (attacker == null || string.IsNullOrWhiteSpace(cooldownGroup))
        {
            return false;
        }

        if (ShouldBypassCooldowns(attacker))
        {
            return false;
        }

        string key = cooldownGroup.Trim();
        if (!Cooldowns.TryGetValue(attacker, out CharacterCooldownState state))
        {
            return false;
        }

        EnsureCurrentApplyRevision(state);
        double now = SecondaryAttackManager.GetNetworkTimeSeconds();
        if (!TryGetActive(state, key, now, out CooldownEntry entry))
        {
            return false;
        }

        remaining = (float)Math.Max(0d, entry.ReadyAt - now);
        return true;
    }

    internal static void CollectHudEntries(
        Player player,
        List<NativeCooldownEntry> entries)
    {
        if (player == null || entries == null)
        {
            return;
        }

        if (ShouldBypassCooldowns(player))
        {
            return;
        }

        if (!Cooldowns.TryGetValue(player, out CharacterCooldownState state))
        {
            return;
        }

        EnsureCurrentApplyRevision(state);
        double now = SecondaryAttackManager.GetNetworkTimeSeconds();
        PruneExpired(state, now);
        foreach (CooldownEntry entry in state.EntriesByGroup.Values)
        {
            float remaining = (float)Math.Max(0d, entry.ReadyAt - now);
            entries.Add(new NativeCooldownEntry(
                entry.Icon,
                remaining,
                entry.Duration, presetKey: entry.PresetKey));
        }
    }

    internal static void ClearAllCooldowns(Character attacker)
    {
        if (!Cooldowns.TryGetValue(attacker, out CharacterCooldownState state))
        {
            return;
        }

        state.EntriesByGroup.Clear();
        state.ApplyRevision = EdgeNativeDefinitions.ApplyRevision;
    }

    private static bool ShouldBypassCooldowns(Character attacker)
    {
        if (!(attacker == Player.m_localPlayer && (Plugin.Settings?.Get(EdgeSettings.Cheats.NoSecondaryCooldowns) ?? false)))
        {
            return false;
        }

        ClearAllCooldowns(attacker);
        return true;
    }

    private static bool TryGetActive(
        CharacterCooldownState state,
        string key,
        double now,
        out CooldownEntry entry)
    {
        if (state.EntriesByGroup.TryGetValue(key, out entry) && now < entry.ReadyAt)
        {
            return true;
        }

        state.EntriesByGroup.Remove(key);
        entry = default;
        return false;
    }

    private static void PruneExpired(CharacterCooldownState state, double now)
    {
        List<string> keys = state.UpdateKeys;
        keys.Clear();
        foreach ((string key, CooldownEntry entry) in state.EntriesByGroup)
        {
            if (entry.ReadyAt <= now)
            {
                keys.Add(key);
            }
        }

        foreach (string key in keys)
        {
            state.EntriesByGroup.Remove(key);
        }

        keys.Clear();
    }

    private static void EnsureCurrentApplyRevision(CharacterCooldownState state)
    {
        int applyRevision = EdgeNativeDefinitions.ApplyRevision;
        if (state.ApplyRevision == applyRevision)
        {
            return;
        }

        state.EntriesByGroup.Clear();
        state.ApplyRevision = applyRevision;
    }

    private readonly struct CooldownEntry
    {
        internal CooldownEntry(double readyAt, float duration, Sprite? icon, string presetKey)
        {
            ReadyAt = readyAt;
            Duration = duration;
            Icon = icon; PresetKey = presetKey;
        }

        internal double ReadyAt { get; }

        internal float Duration { get; }

        internal Sprite? Icon { get; }
        internal string PresetKey { get; }
    }

    private sealed class CharacterCooldownState
    {
        public int ApplyRevision { get; set; }

        public Dictionary<string, CooldownEntry> EntriesByGroup { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> UpdateKeys { get; } = new();
    }
}
