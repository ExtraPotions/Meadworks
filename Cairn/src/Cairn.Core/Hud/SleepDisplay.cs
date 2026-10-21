// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Ember's established sleep HUD label math, presented beside Cairn's clock.
using System;
using System.Globalization;

namespace ExtraPotions.Cairn.Hud
{
    public readonly struct SleepDisplay
    {
        public const string CapabilityId = "meadworks.cairn.sleep_display";
        public static Version ContractVersion { get; } = new Version(1, 0);

        public bool Visible { get; }
        public string Sleeping { get; }
        public string Players { get; }
        public string BoostPercent { get; }
        public string SkipPercent { get; }

        public SleepDisplay(bool windowOpen, int players, int sleeping, double extraRate, bool skipRunning, float skipProgress)
        {
            // Only while someone is in bed (or the skip runs): an open window with nobody sleeping is not worth a line.
            Visible = players > 0 && (skipRunning || (windowOpen && sleeping > 0));
            Players = Math.Max(0, players).ToString(CultureInfo.InvariantCulture);
            Sleeping = Math.Max(0, Math.Min(players, sleeping)).ToString(CultureInfo.InvariantCulture);
            // Ember's label is the share of maximum partial boost, not extra game seconds per second.
            var share = extraRate > 0 && players > 1 ? Math.Min(1.0, sleeping / (double)(players - 1)) : 0.0;
            BoostPercent = Math.Max(0, 100 * share).ToString("F0", CultureInfo.InvariantCulture);
            SkipPercent = skipRunning ? (100 * Math.Max(0, Math.Min(1, skipProgress))).ToString("F0", CultureInfo.InvariantCulture) : "";
        }
    }
}
