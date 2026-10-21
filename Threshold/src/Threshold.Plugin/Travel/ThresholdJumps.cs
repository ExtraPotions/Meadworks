// Threshold - ExtraPotions Meadworks portals.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/QuickJumps/JumpPatches.cs (the UpdateTeleport and
// UpdateBlackScreen prefixes), Wayfare/Wayfare/src/QuickJumps/JumpScreen.cs (Started, Clear, Loading, FadeInFast, FadeOut)
// and Wayfare/Wayfare/src/QuickJumps/JumpTiming.cs (Hurry, startedAt).
// Changed by expDARE, 2026: routed jumps and native tag-paired jumps in Default are hurried (console jumps stay the
// game's); the timer arithmetic moved to Threshold.Core JumpRules (Step, HoldLanding); the private Player and Hud members
// through Harmony AccessTools (no publicizer); no sea gate crew hold; patches installed by Threshold's own Harmony
// instance while Threshold has not stepped aside, acting only while Threshold routes; the sea gate crew hold shares the
// UpdateTeleport prefix, as in Wayfare.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Routing;
using ExtraPotions.Threshold.Runtime.SeaGates;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.Travel
{
    /// <summary>
    /// Quick Portals (U5, section "7 - Jumps"). A long jump runs on the jumping player's own client in
    /// <c>Player.UpdateTeleport</c>: the player moves when the timer passes 2 s and lands once it passes 8 s and the area is
    /// ready. For a jump Threshold started, the seconds come from the map distance (<see cref="JumpRules.Seconds"/>), the
    /// move comes after at most a quarter of a second, and a client's landing waits for the target's objects
    /// (<see cref="ThresholdAreaLoading.Settled"/>) but never beyond the game's own 8 s. Screen Only When Loading keeps the
    /// screen clear for a jump into an area this client has loaded already.
    /// </summary>
    public static class ThresholdJumps
    {
        static readonly AccessTools.FieldRef<Player, bool> Teleporting = AccessTools.FieldRefAccess<Player, bool>("m_teleporting");
        static readonly AccessTools.FieldRef<Player, bool> Distant = AccessTools.FieldRefAccess<Player, bool>("m_distantTeleport");
        static readonly AccessTools.FieldRef<Player, float> Timer = AccessTools.FieldRefAccess<Player, float>("m_teleportTimer");
        static readonly AccessTools.FieldRef<Player, Vector3> From = AccessTools.FieldRefAccess<Player, Vector3>("m_teleportFromPos");
        static readonly AccessTools.FieldRef<Player, Vector3> Target = AccessTools.FieldRefAccess<Player, Vector3>("m_teleportTargetPos");
        static readonly AccessTools.FieldRef<Hud, bool> HaveSetupLoadScreen = AccessTools.FieldRefAccess<Hud, bool>("m_haveSetupLoadScreen");

        /// <summary>The game's fade time when no player state gives another.</summary>
        const float FadeSeconds = 1f;

        /// <summary>The fade to black of a quick jump that loads: done before the move (at most 0.25 s).</summary>
        const float FastFadeSeconds = 0.2f;

        static bool ours;
        static bool clearScreen;
        static float seconds = JumpRules.GameArrival;
        static float startedAt;
        static bool failed;

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Player), "UpdateTeleport", new[] { typeof(float) }),
                prefix: new HarmonyMethod(typeof(ThresholdJumps), nameof(BeforeStep)));
            harmony.Patch(AccessTools.Method(typeof(Hud), "UpdateBlackScreen", new[] { typeof(Player), typeof(float) }),
                prefix: new HarmonyMethod(typeof(ThresholdJumps), nameof(BeforeScreen)));
            harmony.Patch(AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.Teleport), new[] { typeof(Player) }),
                prefix: new HarmonyMethod(typeof(ThresholdJumps), nameof(BeforeNativePortal)),
                postfix: new HarmonyMethod(typeof(ThresholdJumps), nameof(AfterNativePortal)));
            ThresholdAreaLoading.Patch(harmony);
        }

        static void BeforeNativePortal(Player player, out bool __state) => __state = player != null && player == Player.m_localPlayer
            && !player.IsTeleporting() && ThresholdModules.Active() && ThresholdModules.Mode == TeleportMode.Default;

        static void AfterNativePortal(Player player, bool __state)
        {
            if (__state && player != null && player.IsTeleporting()) Started(player);
        }

        /// <summary>The local player's jump Threshold started (after <c>Player.TeleportTo</c> answered true): its seconds
        /// and whether it needs the teleport screen are decided once, so the screen never flickers during it.</summary>
        public static void Started(Player player)
        {
            ours = false;
            var settings = Plugin.Settings;
            if (settings == null || player == null || player != Player.m_localPlayer || failed) return;
            try
            {
                if (!Teleporting(player) || !Distant(player)) return;
                var to = Target(player);
                float metres = JumpRules.MapDistance(Point(From(player)), Point(to));
                seconds = settings.Get(ThresholdSettings.Jumps.QuickPortals)
                    ? JumpRules.Seconds(metres, settings.Get(ThresholdSettings.Jumps.QuickPortalRange), settings.Get(ThresholdSettings.Jumps.QuickPortalSeconds))
                    : JumpRules.GameArrival;
                clearScreen = settings.Get(ThresholdSettings.Jumps.ScreenOnlyWhenLoading) && ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(to);
                startedAt = Time.time;
                ours = true;
                Plugin.Log?.Verbose("Threshold: jump of " + metres.ToString("F0") + " m takes " + seconds.ToString("0.#") + " s"
                    + (clearScreen ? ", target loaded already (no teleport screen)" : ""));
            }
            catch (Exception e) { Fail(e); }
        }

        /// <summary>The local player's jump Threshold started is still running.</summary>
        public static bool Running(Player? player) => ours && player != null && player == Player.m_localPlayer && player.IsTeleporting();

        /// <summary>The running Threshold jump loads behind the teleport screen (Quick Area Loading's reason).</summary>
        public static bool LoadingBehindScreen()
        {
            var player = Player.m_localPlayer;
            var settings = Plugin.Settings;
            return Running(player) && !clearScreen && settings != null && settings.Get(ThresholdSettings.Jumps.QuickAreaLoading);
        }

        /// <summary>The one <c>Player.UpdateTeleport</c> prefix: a sea gate crew hold replaces the game's step while it holds
        /// the local player (<see cref="CrewHold"/>); otherwise a Threshold jump is hurried and the crew hold's
        /// between-jumps work runs.</summary>
        static bool BeforeStep(Player __instance, float dt)
        {
            if (__instance == null || __instance != Player.m_localPlayer) return true;
            if (CrewHold.Holding) return !CrewHold.Step(__instance, dt);
            CrewHold.Between(__instance);
            if (!ours) return true;
            try
            {
                if (!Teleporting(__instance) || !ThresholdModules.Active()) { ours = false; return true; }
                if (!Distant(__instance) || seconds >= JumpRules.GameArrival) return true;
                ref float timer = ref Timer(__instance);
                timer = JumpRules.Step(timer, dt, seconds);
                timer = JumpRules.HoldLanding(timer, dt, Time.time - startedAt, ThresholdAreaLoading.Settled(Target(__instance)));
            }
            catch (Exception e) { Fail(e); }
            return true;
        }

        static bool BeforeScreen(Hud __instance, Player player, float dt)
        {
            if (!ours || __instance == null || !Local(player)) return true;
            try
            {
                if (clearScreen && player.IsTeleporting())
                {
                    HaveSetupLoadScreen(__instance) = false;
                    __instance.m_loadingScreen.alpha = Mathf.MoveTowards(__instance.m_loadingScreen.alpha, 0f, dt / FadeSeconds);
                    if (__instance.m_loadingScreen.alpha <= 0f) __instance.m_loadingScreen.gameObject.SetActive(false);
                    return false;
                }
                if (!clearScreen && seconds < JumpRules.GameArrival && player.ShowTeleportAnimation())
                {
                    __instance.m_loadingScreen.gameObject.SetActive(true);
                    __instance.m_loadingScreen.alpha = Mathf.MoveTowards(__instance.m_loadingScreen.alpha, 1f, dt / FastFadeSeconds);
                }
            }
            catch (Exception e) { Fail(e); }
            return true;
        }

        static bool Local(Player player) => player != null && player == Player.m_localPlayer && !player.IsDead() && !player.IsSleeping();

        static MapPoint Point(Vector3 v) => new MapPoint(v.x, v.y, v.z);

        /// <summary>A game change broke the members: jumps stay the game's for the rest of the process.</summary>
        static void Fail(Exception e)
        {
            ours = false;
            if (failed) return;
            failed = true;
            Plugin.Log?.Error("Threshold: quick jumps are off (the game's jump members changed): " + e.GetType().Name + ": " + e.Message);
        }

        public static void Reset()
        {
            ours = false;
            clearScreen = false;
            seconds = JumpRules.GameArrival;
            ThresholdAreaLoading.Reset();
        }
    }
}
