// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// BackstabSkillGainSystem.cs (owner smoke time-reset field subset; general skill award belongs to Saga). Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, diagnostic and native boundary seams; source algorithms retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using HarmonyLib;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Edge.Secondaries.Adopted;
using ExtraPotions.Edge.Runtime.Quickstep;
internal static class BackstabSkillGainSystem
{
    private static readonly AccessTools.FieldRef<Character, float>? BackstabTimeField = TryCreateBackstabTimeFieldRef();
    internal static bool TryReduceBackstabTime(Character target, float seconds)
    {
        if (target == null || seconds <= 0f || !TryGetBackstabTime(target, out float currentBackstabTime))
        {
            return false;
        }

        try
        {
            BackstabTimeField!(target) = currentBackstabTime - seconds;
            return true;
        }
        catch (System.FieldAccessException)
        {
            return false;
        }
    }

    private static bool TryGetBackstabTime(Character target, out float backstabTime)
    {
        if (target == null || BackstabTimeField == null)
        {
            backstabTime = 0f;
            return false;
        }

        try
        {
            backstabTime = BackstabTimeField(target);
            return true;
        }
        catch (System.FieldAccessException)
        {
            backstabTime = 0f;
            return false;
        }
    }

    private static AccessTools.FieldRef<Character, float>? TryCreateBackstabTimeFieldRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<Character, float>("m_backstabTime");
        }
        catch (System.Exception)
        {
            return null;
        }
    }

}
