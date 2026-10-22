// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Adapters;
using ExtraPotions.Wildblood.Adopted.Breeding;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>Mother-only trait carry around the game's owned birth. No sire, keeper, twins or level producer.</summary>
    [HarmonyPatch(typeof(Procreation), "Procreate")]
    public static class BirthPatch
    {
        private static bool Prefix(Procreation __instance, out Lineage.Call? __state)
        {
            __state = null;
            if (ShepherdBreedingOwnership.FallbackOwnsBirth && !BreedingReadiness.Ready()
                && __instance.m_nview != null && __instance.m_nview.IsValid() && __instance.m_nview.IsOwner()
                && __instance.m_tameable != null && __instance.m_tameable.IsTamed()
                && __instance.IsPregnant() && __instance.IsDue()) return false;
            Lineage.Call? call = null;
            SafeCall.Run("Procreation minimum trait carry", () => call = Hold(__instance));
            __state = call;
            return true;
        }

        private static Lineage.Call? Hold(Procreation __instance)
        {
            if (!ShepherdBreedingOwnership.FallbackActive || !Parents.IsBirthNow(__instance)
                || __instance.m_offspring == null) return null;
            Procreation parent = __instance;
            return Lineage.Begin(level => NativeTraitsCapability.Inherit(
                NativeTraitsCapability.Read(parent.m_character), "", level, 100f,
                UnityEngine.Random.Range(0, int.MaxValue)), parent.m_offspring.name, watchEggs: true);
        }
        private static void Postfix(Lineage.Call? __state) => Lineage.Complete(__state);
        private static Exception? Finalizer(Exception? __exception, Lineage.Call? __state)
        { Lineage.End(__state); return __exception; }
    }

    /// <summary>Records only the matching item waking inside the current minimum birth context.</summary>
    [HarmonyPatch(typeof(ItemDrop), "Awake")]
    public static class EggLaidPatch
    {
        private static void Postfix(ItemDrop __instance) => Lineage.Notice(__instance);
    }

    /// <summary>Carry the young's blob unchanged; native GrowUpdate supplies the adult's level.</summary>
    [HarmonyPatch(typeof(Growup), "GrowUpdate")]
    public static class GrowUpPatch
    {
        private static void Prefix(Growup __instance, out Lineage.Call? __state)
        {
            Lineage.Call? call = null;
            SafeCall.Run("Growup minimum trait carry", () => call = Hold(__instance));
            __state = call;
        }

        private static Lineage.Call? Hold(Growup __instance)
        {
            if (!ShepherdBreedingOwnership.FallbackActive || __instance.m_nview == null
                || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || __instance.m_baseAI == null
                || __instance.m_grownPrefab == null
                || __instance.m_baseAI.GetTimeSinceSpawned().TotalSeconds <= __instance.m_growTime) return null;
            Character? young = __instance.GetComponent<Character>();
            if (young == null) return null;
            string blob = NativeTraitsCapability.Read(young);
            return Lineage.Begin(_ => blob, __instance.m_grownPrefab.name);
        }
        private static void Postfix(Lineage.Call? __state) => Lineage.Complete(__state);
        private static Exception? Finalizer(Exception? __exception, Lineage.Call? __state)
        { Lineage.End(__state); return __exception; }
    }

    /// <summary>Carry canonical or legacy egg traits; the game's hatch level and saved egg record stay untouched.</summary>
    [HarmonyPatch(typeof(EggGrow), "GrowUpdate")]
    public static class HatchPatch
    {
        private static void Prefix(EggGrow __instance, out Lineage.Call? __state)
        {
            Lineage.Call? call = null;
            SafeCall.Run("EggGrow minimum trait carry", () => call = Hold(__instance));
            __state = call;
        }

        private static Lineage.Call? Hold(EggGrow __instance)
        {
            if (!ShepherdBreedingOwnership.FallbackActive || __instance.m_nview == null
                || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || __instance.m_item == null
                || __instance.m_grownPrefab == null) return null;
            var item = __instance.m_item.m_itemData;
            string blob = NativeTraitsCapability.EggDecode(NativeTraitsCapability.EggEncode(NativeTraitsCapability.Read(item)));
            return Lineage.Begin(_ => blob, __instance.m_grownPrefab.name);
        }
        private static void Postfix(Lineage.Call? __state) => Lineage.Complete(__state);
        private static Exception? Finalizer(Exception? __exception, Lineage.Call? __state)
        { Lineage.End(__state); return __exception; }
    }
}
