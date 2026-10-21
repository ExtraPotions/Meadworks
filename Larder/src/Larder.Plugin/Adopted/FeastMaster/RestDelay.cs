// Adopted from ValheimMods OpenKeep/OpenKeep/src/Homestead/RestPatch.cs,
// OpenKeep/OpenKeep/src/Homestead/RestDelay.cs and OpenKeep/OpenKeep/src/Homestead/RestSettings.cs
// at 3bb778330f23645ccddb42fbc0c6ffab72c842b6.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: native default, Recovery authority/source gates and per-call restoration.
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    public static class RestDelay
    {
        public static ConfigEntry<float> Seconds { get; private set; }

        public static bool Bind(ObjectDB db)
        {
            if (Seconds != null || !(db.GetStatusEffect(SEMan.s_statusEffectResting) is SE_Cozy resting)
                || float.IsNaN(resting.m_delay) || float.IsInfinity(resting.m_delay)
                || resting.m_delay < 0f || resting.m_delay > 60f)
                return false;
            Seconds = NativeFeastMaster.Bindings.Bind(Settings.StaminaRegenSection, "Rested Delay", resting.m_delay,
                "Seconds of resting before Rested is granted. Fire and resting conditions still apply. Default follows the game's Resting effect.",
                acceptableValues: new AcceptableValueRange<float>(0f, 60f));
            return true;
        }

        internal static void Reset() => Seconds = null;
    }

    [HarmonyPatch(typeof(SE_Cozy), nameof(SE_Cozy.UpdateStatusEffect))]
    public static class RestDelayPatch
    {
        public struct DelayState
        {
            public bool Changed;
            public float Original;
            public float Applied;
        }

        public static bool Prepare() => NativeFeastMaster.Active("Recovery")
            && !Plugin.RestDelaySteppedAside && Customized.Any(RestDelay.Seconds);

        [HarmonyPrefix]
        public static void Prefix(SE_Cozy __instance, out DelayState __state)
        {
            __state = default;
            if (!Prepare() || !__instance || __instance.NameHash() != SEMan.s_statusEffectResting
                || (ObjectDB.instance && ReferenceEquals(__instance, ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectResting))))
                return;
            float seconds = NativeFeastMaster.Value(RestDelay.Seconds);
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
                return;
            float applied = Mathf.Clamp(seconds, 0f, 60f);
            __state = new DelayState { Changed = true, Original = __instance.m_delay, Applied = applied };
            __instance.m_delay = applied;
        }

        [HarmonyFinalizer]
        public static void Finalizer(SE_Cozy __instance, DelayState __state)
        {
            if (__state.Changed && __instance && __instance.m_delay == __state.Applied)
                __instance.m_delay = __state.Original;
        }
    }
}
