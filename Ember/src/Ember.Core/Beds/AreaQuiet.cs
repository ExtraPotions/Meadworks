// Adapted from ValheimModLibs/AreaLoading/AreaSettle.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 684671f98de54db6ea6652092ec041f44016c37d
// Changed by expDARE, 2026: Unity-free sampled quiet state; runtime owns target and world resets.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Ember.Beds
{
    public sealed class AreaQuiet
    {
        public const double CheckSeconds = 0.1d, QuietSeconds = 0.5d;
        private bool sampled;
        private int count;
        private long ids, revisions;
        private double lastSample, quietSince;
        public bool Settled { get; private set; }

        public bool Due(double now) => !Finite(now) || !sampled || now < lastSample || now - lastSample >= CheckSeconds;

        public bool Observe(double now, int objectCount, long objectIds, long objectRevisions)
        {
            if (!Finite(now) || objectCount < 0) { Reset(); return false; }
            if (sampled && now < lastSample) Reset();
            if (!Due(now)) return Settled;
            bool same = sampled && objectCount == count && objectIds == ids && objectRevisions == revisions;
            sampled = true;
            lastSample = now;
            count = objectCount;
            ids = objectIds;
            revisions = objectRevisions;
            if (!same) quietSince = now;
            Settled = same && now - quietSince >= QuietSeconds;
            return Settled;
        }

        public void Reset()
        {
            sampled = false;
            Settled = false;
            count = 0;
            ids = revisions = 0;
            lastSample = quietSince = 0d;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
