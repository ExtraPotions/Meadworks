// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Fight/Strike.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Setting the hook, on the angler's client. A fish that reaches the float nibbles it (Fish sends RPC_Nibble to the
    /// float's owner); with the right bait the float bobs and the fish is remembered. The game hooks it if the angler
    /// reels within 0.5 s (FishingFloat.TryToHook, called while reeling). For the local player's float:
    /// <list type="bullet">
    /// <item>The window grows with the angler's level (Strike Window At 0 to At 100).</item>
    /// <item>Starting to reel within the Perfect Strike Window of the nibble, when not already reeling as it nibbled, is a
    /// perfect strike: the fish skips the thrash the hook starts (<see cref="Tiring"/>), and "Perfect strike!" floats up.</item>
    /// <item>A nibble tells the angler what nibbles, from the senses' levels (<see cref="Sense"/>); a snagged hook takes no
    /// nibbles (<see cref="Snags"/>).</item>
    /// <item>A fresh hook may be a big one (<see cref="BigOne"/>).</item>
    /// </list>
    /// The hook itself is the game's: its message, SetCatch (the fish is claimed and starts its first thrash) and stat.
    /// </summary>
    public static class Strike
    {
        [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.TryToHook))]
        private static class Hook
        {
            [HarmonyPrefix]
            private static bool Prefix(FishingFloat __instance)
            {
                FloatFight fight = FishSkill.Active ? FloatFight.Of(__instance) : null;
                if (fight == null)
                    return true;
                return !HookGuard.Run("strike", static strike => TryHook(strike.fishingFloat, strike.fight), (fishingFloat: __instance, fight), false);
            }
        }

        [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.RPC_Nibble))]
        private static class Nibble
        {
            [HarmonyPrefix]
            private static bool Prefix(FishingFloat __instance, out float __state)
            {
                __state = __instance.m_nibbleTime;
                return !(FishSkill.Active && Snags.BlocksNibble(FloatFight.Of(__instance)));
            }

            [HarmonyPostfix]
            private static void Postfix(FishingFloat __instance, bool correctBait, float __state)
            {
                if (correctBait && __instance.m_nibbler != null && __instance.m_nibbleTime != __state && FishSkill.Active)
                    HookGuard.Run("nibble", static fishingFloat => OnNibble(fishingFloat), __instance);
            }
        }

        /// <summary>The strike window for an angler's level, in seconds.</summary>
        public static float Window(float level) =>
            Mathf.Max(0.05f, FishSkill.Between(FishingFightSettings.StrikeWindowAt0.Value, FishingFightSettings.StrikeWindowAt100.Value, level));

        /// <summary>The game's TryToHook with the angler's window; always handles the call.</summary>
        private static bool TryHook(FishingFloat fishingFloat, FloatFight fight)
        {
            Fish nibbler = fishingFloat.m_nibbler;
            if (nibbler == null || fishingFloat.GetCatch() != null || OnAnotherLine(nibbler))
                return true;
            float since = Time.time - fishingFloat.m_nibbleTime;
            if (since >= Window(fight.Level))
                return true;
            Player angler = Angler.LocalOf(fishingFloat);
            bool reeling = angler != null && angler.IsBlocking();
            bool perfect = reeling && !fight.ReelingAtNibble && since <= FishingFightSettings.PerfectStrikeWindow.Value;
            fight.Hooked(nibbler, perfect);
            fishingFloat.Message("$msg_fishing_hooked", prioritized: true);
            fishingFloat.SetCatch(nibbler);
            fishingFloat.m_nibbler = null;
            Game.instance.IncrementPlayerStat(PlayerStatType.FishHooked);
            if (perfect)
                FishCallout.ShowLocal(fishingFloat.transform.position + Vector3.up, SagaText.Text("gs_3bfabb9776423816"));
            HookGuard.Run("big one", () => BigOne.OnHooked(fishingFloat, fight, nibbler));
            return true;
        }

        /// <summary>
        /// Whether the nibbler was hooked on someone else's line since it nibbled here: a strike window longer than the
        /// game's leaves time for that, and hooking it again would pull it from the other float.
        /// </summary>
        private static bool OnAnotherLine(Fish nibbler) =>
            nibbler.m_nview == null || !nibbler.m_nview.IsValid() || nibbler.m_nview.GetZDO().GetInt(ZDOVars.s_hooked) != 0;

        private static void OnNibble(FishingFloat fishingFloat)
        {
            FloatFight fight = FloatFight.Of(fishingFloat);
            Player angler = fight != null ? Angler.LocalOf(fishingFloat) : null;
            if (angler == null)
                return;
            fight.ReelingAtNibble = angler.IsBlocking();
            Sense.OnNibble(fight, fishingFloat.m_nibbler, angler);
        }
    }
}
