// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Sleep In Any Bed (October 20, 2026 playtest).
using System;
using ExtraPotions.Ember.Config;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>
    /// The game's Bed.Interact ignores a bed claimed by another player (no prompt, no action). With Sleep In Any Bed on,
    /// the local player may sleep in it: the same time, enemy, roof, fire and wet checks as the owner's own sleep, then
    /// the native bed attach (which sets the player's inBed flag that the sleep count reads). Ownership and the player's
    /// spawn point are not touched. Unclaimed and own beds keep the native path.
    /// </summary>
    [HarmonyPatch(typeof(Bed))]
    internal static class SleepInAnyBed
    {
        static bool warned;

        internal static bool Allowed()
        {
            if (!EmberModules.ProductActive(out _) || EmberModules.StepAside.SteppedAside(ExtraPotions.Ember.Integration.EmberModule.Sleep)
                || Plugin.Settings == null) return false;
            return Plugin.Settings.Get(EmberSettings.Sleep.SleepInAnyBed);
        }

        /// <summary>A bed claimed by someone else that the local player may use now.</summary>
        static bool OthersBed(Bed bed)
        {
            if (!bed || !bed.m_nview || !bed.m_nview.IsValid() || Game.instance == null) return false;
            return bed.GetOwner() != 0L && !bed.IsMine() && Allowed();
        }

        [HarmonyPatch(nameof(Bed.Interact)), HarmonyPrefix, HarmonyPriority(Priority.Low)]
        static bool Interact(Bed __instance, Humanoid human, bool repeat, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal || repeat) return true;
            try
            {
                if (!(human is Player player) || player != Player.m_localPlayer || !OthersBed(__instance)) return true;
                __result = false;
                if (!EnvMan.CanSleep())
                {
                    player.Message(MessageHud.MessageType.Center, "$msg_cantsleep");
                    return false;
                }
                if (!__instance.CheckEnemies(player) || !__instance.CheckExposure(player) || !__instance.CheckFire(player) || !__instance.CheckWet(player))
                    return false;
                player.AttachStart(__instance.m_spawnPoint, __instance.gameObject, true, true, false, "attach_bed", new Vector3(0f, 0.5f, 0f));
                return false;
            }
            catch (Exception error)
            {
                // Changed native members keep the game's own behavior; report once per process.
                if (!warned) { warned = true; Debug.LogWarning("[Ember] Sleep In Any Bed skipped: " + error.GetType().Name + ": " + error.Message); }
                return true;
            }
        }

        [HarmonyPatch(nameof(Bed.GetHoverText)), HarmonyPostfix]
        static void HoverText(Bed __instance, ref string __result)
        {
            try
            {
                if (string.IsNullOrEmpty(__result) || !OthersBed(__instance)) return;
                __result += global::Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] $piece_bed_sleep");
            }
            catch (Exception)
            {
                // Hover text is cosmetic; the native text stays.
            }
        }
    }
}
