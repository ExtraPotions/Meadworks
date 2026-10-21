// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Magpie.Pickup
{
    /// <summary>
    /// Design A4 (SwiftPickup <c>ScanIntervalSeconds</c>, <c>MaxPickupsPerScan</c>, behavior): search for items once per
    /// Scan Interval, take at most Max Pickups Per Scan items per search (0 = no limit) and never more than one per frame.
    /// </summary>
    public sealed class PickupScheduler
    {
        private double nextScan = double.NegativeInfinity;
        private int remaining;
        private bool unlimited;
        private int lastTakeFrame = int.MinValue;

        public bool ScanDue(double now, float interval)
        {
            // Tolerance: the interval comes in as a float (0.15f is slightly above 0.15).
            if (now + 1e-6 < nextScan) return false;
            nextScan = now + Math.Max(0.0, interval);
            return true;
        }

        public void BeginScan(int budget)
        {
            unlimited = budget <= 0;
            remaining = Math.Max(0, budget);
        }

        public bool TryTake(int frame)
        {
            if (frame == lastTakeFrame || (!unlimited && remaining <= 0)) return false;
            lastTakeFrame = frame;
            if (!unlimited) remaining--;
            return true;
        }

        public void Reset()
        {
            nextScan = double.NegativeInfinity;
            remaining = 0;
            unlimited = false;
            lastTakeFrame = int.MinValue;
        }
    }
}
