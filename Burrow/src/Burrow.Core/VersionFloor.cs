// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Burrow
{
    /// <summary>The oldest Burrow version a host lets join: same major.minor, any patch (batch 4A, patch-lower joins).</summary>
    public static class VersionFloor
    {
        public static string OldestAccepted(string version)
        {
            Version parsed = new Version(version);
            return parsed.Major + "." + parsed.Minor + ".0";
        }
    }
}
