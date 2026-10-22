// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Mantle.Trinkets
{
    public static class KillGain
    {
        public static float Calculate(float fraction, float effectiveMaximum, float targetMultiplier)
        {
            if (!Finite(fraction) || fraction <= 0f || fraction > 0.10f ||
                !Finite(effectiveMaximum) || effectiveMaximum <= 0f ||
                !Finite(targetMultiplier) || targetMultiplier <= 0f)
                return 0f;
            double gain = (double)fraction * effectiveMaximum * targetMultiplier;
            return gain <= float.MaxValue ? (float)gain : 0f;
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class KillEligibility
    {
        public static bool IsEligible(bool serverValidated, bool unambiguousAttribution, bool hasNativeTrinket,
            bool isPlayer, bool isTamed, bool isHostile, float targetMultiplier, long creditedKiller, long recipient)
        {
            return serverValidated && unambiguousAttribution && hasNativeTrinket && !isPlayer && !isTamed &&
                isHostile && KillGain.Finite(targetMultiplier) && targetMultiplier > 0f &&
                creditedKiller != 0 && creditedKiller == recipient;
        }
    }

    public sealed class DeathCreditLedger
    {
        private readonly HashSet<string> deaths = new HashSet<string>(StringComparer.Ordinal);
        private readonly int capacity;
        private readonly object gate = new object();

        public DeathCreditLedger(int capacity = 1000000)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        public int Count { get { lock (gate) return deaths.Count; } }

        public bool TryClaim(string deathId, long killer)
        {
            if (string.IsNullOrWhiteSpace(deathId) || deathId.Length > 256 || killer == 0) return false;
            // Never evict a credited death: delayed packets must not become new awards.
            lock (gate) return deaths.Count < capacity && deaths.Add(deathId);
        }

        public void Clear() { lock (gate) deaths.Clear(); }
    }
}
