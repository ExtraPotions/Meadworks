// SPDX-License-Identifier: GPL-3.0-only
// Adapted from BossRules 1.1.8 (sighsorry1029), ae28779955a8979a96a43de373504d3a35f6868f.
// Changed by expDARE, 2026: fixed generated-default policy and world/session-bound ephemeral leases.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Bosses;

public static class TamedPressure
{
    public const float Range = 32f, DamageFraction = .01f, Incoming = 1.25f, Outgoing = .75f;
    public const int MaxTargets = 4;
    public const double ScanSeconds = 5d, DamageSeconds = 1d, LeaseSeconds = 5.5d;

    public static bool Eligible(bool valid, bool alive, bool player, bool tamed, bool monsterAi) =>
        valid && alive && !player && tamed && monsterAi;

    public static bool InRange(float x, float z, float targetX, float targetZ)
    {
        double dx = (double)x - targetX, dz = (double)z - targetZ;
        return dx * dx + dz * dz <= Range * Range;
    }

    public static bool ActiveLease(double until, double now, long world, long currentWorld, string epoch, string currentEpoch) =>
        !double.IsNaN(until) && !double.IsInfinity(until) && !double.IsNaN(now) && !double.IsInfinity(now)
        && until > now && until <= now + LeaseSeconds + .1d && world != 0 && world == currentWorld
        && !string.IsNullOrEmpty(epoch) && string.Equals(epoch, currentEpoch, StringComparison.Ordinal);

    public static float Multiplier(bool hasAttacker, bool incoming, bool outgoing) =>
        !hasAttacker ? 1f : (incoming ? Incoming : 1f) * (outgoing ? Outgoing : 1f);
}

public sealed class TamedPressureClock<T> where T : notnull
{
    sealed class Target
    {
        public double Until;
        public long Owner;
        public float Pending;
    }
    readonly Dictionary<T, Target> targets = new();
    double nextScan, nextDamage;
    public bool ScanDue(double now)
    {
        if (!Finite(now) || now < nextScan) return false;
        nextScan = now + TamedPressure.ScanSeconds;
        return true;
    }
    public bool DamageDue(double now)
    {
        if (!Finite(now) || now < nextDamage) return false;
        nextDamage = now + TamedPressure.DamageSeconds;
        return true;
    }
    public double Refresh(T id, double now)
    {
        if (!targets.TryGetValue(id, out var target) || target.Until <= now)
            targets[id] = target = new Target();
        return target.Until = now + TamedPressure.LeaseSeconds;
    }

    public void ClearDamage(T id)
    {
        if (targets.TryGetValue(id, out var target)) { target.Pending = 0f; target.Owner = 0; }
    }

    public bool TryDamage(T id, double now, long owner, float amount, int maximumPlayers,
        int currentDifficulty, float healthScalePerPlayer, float playerDamageRate, out float damage)
    {
        damage = 0f;
        if (!targets.TryGetValue(id, out var target)) return false;
        if (!Finite(now) || target.Until <= now) { targets.Remove(id); return false; }
        if (owner == 0 || !Positive(amount) || maximumPlayers < 1 || currentDifficulty < 1
            || !Finite(healthScalePerPlayer) || healthScalePerPlayer < 0f || !Positive(playerDamageRate))
        { ClearDamage(id); return false; }

        int worstPlayers = Math.Max(1, Math.Max(maximumPlayers, currentDifficulty));
        float denominator = 1f + (worstPlayers - 1) * healthScalePerPlayer;
        float enemyScale = 1f / denominator;
        float effectiveScale = enemyScale * playerDamageRate;
        if (!Positive(denominator) || !Positive(enemyScale) || !Positive(effectiveScale))
        { ClearDamage(id); return false; }
        if (target.Owner != owner) { target.Pending = 0f; target.Owner = owner; }
        float pending = target.Pending + amount;
        if (!Positive(pending)) { ClearDamage(id); return false; }
        // Native ApplyDamage scales in this order, then discards totals <= 0.1f.
        float effective = pending * enemyScale;
        effective *= playerDamageRate;
        if (!Finite(effective)) { ClearDamage(id); return false; }
        target.Pending = pending;
        if (effective <= 0.1f) return false;
        damage = pending;
        target.Pending = 0f; // Consume before the potentially reentrant owner RPC; never replay an uncertain send.
        return true;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Positive(float value) => Finite(value) && value > 0f;
    public T[] Current(double now)
    {
        foreach (var pair in targets.ToArray()) if (pair.Value.Until <= now) targets.Remove(pair.Key);
        return targets.Keys.ToArray();
    }
    public void Remove(T id) => targets.Remove(id);
    public void Reset() { targets.Clear(); nextScan = nextDamage = 0d; }
    static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
