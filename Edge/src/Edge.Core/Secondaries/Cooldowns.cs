// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// MeleePresetCooldownSystem.cs and RangedSecondaryCooldownSystem.cs (cooldown * (1 - clamp01(skill/100) *
// clamp01(factor)), never below 0), SecondaryAttackCooldownGroupResolver.cs (explicit group, then automatic family
// group, then preset:<domain>:<preset> fallback), SecondaryCooldownGroupSystem.cs (one entry per trimmed group; an
// active group refuses; a zero duration leaves no entry; ready-at and duration kept for the HUD).
// Changed by expDARE, 2026: Unity-free and per attacker (the plugin keeps one table per local player), time passed in
// instead of read from ZNet, the admin bypass became the server-bound No Secondary Cooldowns cheat (design D2).
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Edge.Secondaries
{
    public static class Cooldowns
    {
        /// <summary>The cooldown after skill reduction; 0 while No Secondary Cooldowns is in effect.</summary>
        public static float Final(float baseCooldown, float skillLevel, float reductionFactor, bool noCooldowns)
        {
            if (noCooldowns) return 0f;
            var reduction = Clamp01(skillLevel / 100f) * Clamp01(reductionFactor);
            return Math.Max(0f, Math.Max(0f, baseCooldown) * (1f - reduction));
        }

        /// <summary>The group a preset's cooldown is kept in.</summary>
        public static string Group(string? explicitGroup, string? automaticFamilyGroup, string domain, string? presetKey)
        {
            if (!string.IsNullOrWhiteSpace(explicitGroup)) return explicitGroup!.Trim();
            if (!string.IsNullOrWhiteSpace(automaticFamilyGroup)) return automaticFamilyGroup!.Trim();
            return PresetFallback(domain, presetKey);
        }

        public static string PresetFallback(string? domain, string? presetKey)
        {
            var d = Token(domain);
            var p = Token(presetKey);
            return d.Length == 0 || p.Length == 0 ? "" : "preset:" + d + ":" + p;
        }

        private static string Token(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value!.Trim().ToLowerInvariant();

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Min(1f, Math.Max(0f, v));
    }

    /// <summary>One attacker's cooldowns by group (network time in seconds). Main thread only.</summary>
    public sealed class CooldownTable
    {
        private readonly Dictionary<string, (double ReadyAt, float Duration)> entries = new Dictionary<string, (double, float)>(StringComparer.Ordinal);

        /// <summary>Starts the group's cooldown when it is ready; false while it is still cooling.</summary>
        public bool TryConsume(string? group, double now, float duration)
        {
            if (string.IsNullOrWhiteSpace(group)) return true;
            var key = group!.Trim();
            if (Active(key, now, out _)) return false;
            entries.Remove(key);
            var d = Math.Max(0f, duration);
            if (d <= 0f) return true;
            entries[key] = (now + d, d);
            return true;
        }

        public bool IsReady(string? group, double now) => string.IsNullOrWhiteSpace(group) || !Active(group!.Trim(), now, out _);

        public float Remaining(string? group, double now) =>
            !string.IsNullOrWhiteSpace(group) && Active(group!.Trim(), now, out var e) ? (float)Math.Max(0d, e.ReadyAt - now) : 0f;

        /// <summary>Elapsed share of the running cooldown, 0 to 1; 1 when ready.</summary>
        public float Progress(string? group, double now)
        {
            if (string.IsNullOrWhiteSpace(group) || !Active(group!.Trim(), now, out var e) || e.Duration <= 0f) return 1f;
            return (float)Math.Min(1d, Math.Max(0d, 1d - (e.ReadyAt - now) / e.Duration));
        }

        public void Clear() => entries.Clear();

        private bool Active(string key, double now, out (double ReadyAt, float Duration) entry) =>
            entries.TryGetValue(key, out entry) && entry.ReadyAt > now;
    }
}
