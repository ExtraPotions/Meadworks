// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackWarningLog.cs. Source copyright and licence retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, catalog and diagnostics seams; Unity rotation value replaced by a portable
// RotationOffset; world-applied snapshot omitted; generated clone names use Edge. Normalisation rules retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Edge.Secondaries.Adopted;

internal static class SecondaryAttackWarningLog
{
    private static readonly HashSet<string> ReportedWarnings = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ReportedIssues = new(StringComparer.OrdinalIgnoreCase);

    internal static void Reset() { ReportedWarnings.Clear(); ReportedIssues.Clear(); }

    internal static bool TryMarkWarning(string key)
    {
        return ReportedWarnings.Add(key);
    }

    internal static bool TryMarkIssue(string key)
    {
        return ReportedIssues.Add(key);
    }

    internal static void WarnOnce(string key, string message, bool emit = true)
    {
        if (emit && TryMarkWarning(key))
        {
            RuleDiagnostics.Warning(message);
        }
    }
}
