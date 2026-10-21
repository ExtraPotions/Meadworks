// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// ThrowProjectileVisualSpin.cs. Source copyright and licence retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace ExtraPotions.Edge.Secondaries.Adopted;

internal static class ProjectileSpinAxis
{
    internal const string None = "none";
    internal const string Horizontal = "horizontal";
    internal const string Vertical = "vertical";

    internal static string Normalize(string? raw)
    {
        string value = raw?.Trim() ?? "";
        if (value.Length == 0)
        {
            return "";
        }

        if (value.Equals(None, System.StringComparison.OrdinalIgnoreCase))
        {
            return None;
        }

        if (value.Equals(Horizontal, System.StringComparison.OrdinalIgnoreCase))
        {
            return Horizontal;
        }

        return value.Equals(Vertical, System.StringComparison.OrdinalIgnoreCase) ? Vertical : "";
    }

}
