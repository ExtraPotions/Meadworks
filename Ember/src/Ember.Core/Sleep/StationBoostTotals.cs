// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Ember.Sleep
{
    public readonly struct StationBoostTotals : IEquatable<StationBoostTotals>
    {
        public StationBoostTotals(double mead, double crops, double pickables)
        { Mead = mead; Crops = crops; Pickables = pickables; }
        public double Mead { get; }
        public double Crops { get; }
        public double Pickables { get; }
        public StationBoostTotals Add(double applied, bool mead, bool crops, bool pickables) => new StationBoostTotals(
            StationTimers.AddBoost(Mead, mead ? applied : 0),
            StationTimers.AddBoost(Crops, crops ? applied : 0),
            StationTimers.AddBoost(Pickables, pickables ? applied : 0));
        public bool Equals(StationBoostTotals other) => Mead == other.Mead && Crops == other.Crops && Pickables == other.Pickables;
        public override bool Equals(object? obj) => obj is StationBoostTotals other && Equals(other);
        public override int GetHashCode() => Mead.GetHashCode() ^ Crops.GetHashCode() ^ Pickables.GetHashCode();

        // The leading total detects a companion file from a different save without changing the legacy total file.
        public string Save(double total) => StationTimers.FormatTotal(total) + "|" + StationTimers.FormatTotal(Mead) + "|" + StationTimers.FormatTotal(Crops) + "|" + StationTimers.FormatTotal(Pickables);
        public static bool TryLoad(string? text, double total, out StationBoostTotals totals)
        {
            totals = default;
            var fields = text?.Split('|');
            if (fields == null || fields.Length != 4 || !StationTimers.TryParseTotal(fields[0], out var saved) || saved != total ||
                !StationTimers.TryParseTotal(fields[1], out var mead) || mead > total ||
                !StationTimers.TryParseTotal(fields[2], out var crops) || crops > total ||
                !StationTimers.TryParseTotal(fields[3], out var pickables) || pickables > total) return false;
            totals = new StationBoostTotals(mead, crops, pickables);
            return true;
        }
    }
}
