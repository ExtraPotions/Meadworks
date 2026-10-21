// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8.
// Changed by expDARE, 2026: product settings, namespace and shared presentation boundaries; source algorithms retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Secondaries.Adopted;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;

internal static class ShieldAccess
{
    private static readonly FieldInfo? SeManCharacterField = AccessTools.Field(typeof(SEMan), "m_character");
    private static readonly FieldInfo? ShieldTotalAbsorbDamageField = AccessTools.Field(typeof(SE_Shield), "m_totalAbsorbDamage");
    private static readonly FieldInfo? ShieldAbsorbedDamageField = AccessTools.Field(typeof(SE_Shield), "m_damage");

    internal static Character? GetSeManCharacter(SEMan seMan)
    {
        return SeManCharacterField?.GetValue(seMan) as Character;
    }

    internal static bool TryReadRemaining(SE_Shield shield, out float remaining, out float remainingTime)
    {
        remaining = 0f;
        remainingTime = 0f;
        if (shield == null || ShieldTotalAbsorbDamageField == null || ShieldAbsorbedDamageField == null)
        {
            return false;
        }

        if (ShieldTotalAbsorbDamageField.GetValue(shield) is not float totalAbsorb ||
            ShieldAbsorbedDamageField.GetValue(shield) is not float absorbed)
        {
            return false;
        }

        remaining = Mathf.Max(0f, totalAbsorb - absorbed);
        remainingTime = Mathf.Max(0f, shield.GetRemaningTime());
        return remaining > 0f && remainingTime > 0f;
    }
}
