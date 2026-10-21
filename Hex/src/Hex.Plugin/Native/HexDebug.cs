// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API adapter. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexDebug
{
    // Timber owns the debug build mode while it is installed (meadworks.owns.debug_build, presence, as the plugin check was).
    static readonly FeatureLink buildOwner = new FeatureLink(FeatureOwnerContract.Id(FeatureOwnerContract.DebugBuild));
    internal static bool TimberInstalled => buildOwner.TryOwned(CruciblePlugin.IsReady ? CruciblePlugin.Services.Capabilities : null, out _);
    static Player? actor;
    static ZNet? net;
    static long generation = -1;
    static bool ownsBuild, savedBuild, appliedBuild;
    // Shift+C: the local time override lives in HexLocalTime; the field of view is restored only while it still holds Hex's value.
    static bool viewing, viewAllowed;
    static GameCamera? viewCamera;
    static float savedFov = -1f, appliedFov = -1f;
    internal static bool Mode => Plugin.Settings?.Effective("debug_mode", false) == true;
    internal static bool Tools => Plugin.Ready && Plugin.Active && Plugin.Settings?.Enabled.Value == true
        && MeadworksAuthority.ProductActive(Plugin.Guid, out _)
        && (Plugin.Settings.DebugTools.Value || Mode) && HexMovement.Owned(Player.m_localPlayer);
    internal static void Initialize()
    {
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Debug.Fly", MischiefCategory.Personal);
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Debug.Build", MischiefCategory.Economy);
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Debug.Kill", MischiefCategory.World);
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Debug.Drops", MischiefCategory.World);
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Debug.Teleport", MischiefCategory.Personal);
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Debug.View", MischiefCategory.Personal);
    }
    static MischiefCategory Category(string action) => action == "fly" || action == "teleport" || action == "view" ? MischiefCategory.Personal
        : action == "build" ? MischiefCategory.Economy : MischiefCategory.World;
    static string Id(string action) => action == "fly" ? "Debug.Fly" : action == "build" ? "Debug.Build"
        : action == "kill" ? "Debug.Kill" : action == "teleport" ? "Debug.Teleport" : action == "view" ? "Debug.View" : "Debug.Drops";
    static bool NativeDebug => Player.m_debugMode && Console.instance != null && Console.instance.IsCheatsEnabled();
    static bool Permit(string action) => Tools && MeadworksMischief.TryUse(Plugin.Guid, Id(action), Category(action), HexText.Text("debug_" + action));
    static void Capture()
    {
        actor = Player.m_localPlayer; net = ZNet.instance; generation = CruciblePlugin.Session.Generation;
    }
    internal static string Execute(string action, bool confirm = false)
    {
        if (action != "fly" && action != "build" && action != "kill" && action != "drops") return HexText.Text("debug_help");
        if (!Tools) return HexText.Text("debug_unavailable");
        if (!Permit(action)) return HexText.Text("actions_denied");
        var currentNet = ZNet.instance;
        if (currentNet == null) return HexText.Text("debug_unavailable");
        if (actor != null && (actor != Player.m_localPlayer || net != currentNet || generation != CruciblePlugin.Session.Generation)) Reset();
        if (action == "build" && TimberInstalled) return HexText.Text("debug_timbers_owner");
        if (action == "fly")
        {
            var setting = Plugin.Settings!.PreferenceEntries()["fly"];
            return Plugin.Settings.SetPreference("fly", !(bool)setting.BoxedValue) ? HexText.Text("debug_applied") : HexText.Text("actions_denied");
        }
        if (!currentNet.IsServer())
        {
            if (action == "build") return HexText.Text("debug_timbers_owner");
            // Kill and drops go to the server, which hands each target to the client that owns it.
            return confirm ? HexRemoteDebug.Request(action == "kill" ? HexOwnerAction.Kill : HexOwnerAction.Drops) : HexText.Text("debug_confirm_required");
        }
        if (action == "build")
        {
            var player = Player.m_localPlayer;
            if (actor != null && actor != player) Reset();
            if (ownsBuild && player.m_noPlacementCost != appliedBuild) ownsBuild = false;
            if (!ownsBuild) { Capture(); savedBuild = player.m_noPlacementCost; }
            appliedBuild = !player.m_noPlacementCost;
            ownsBuild = true;
            try { player.SetNoPlacementCost(appliedBuild); }
            catch { ReleaseBuild(); return HexText.Text("debug_native_unavailable"); }
            return HexText.Text("debug_applied");
        }
        if (!confirm) return HexText.Text("debug_confirm_required");
        // Native admission, cheat confirmation, owner routing and exact native selection remain authoritative.
        var console = Console.instance;
        if (console == null) return HexText.Text("debug_native_unavailable");
        try
        {
            var name = action == "kill" ? "killenemies" : "removedrops";
            if (!Terminal.commands.TryGetValue(name, out var command) || command.action == null
                || command.action.Method.Module.Assembly != typeof(Terminal).Assembly) return HexText.Text("debug_native_unavailable");
            var valid = command.IsValid(console);
            console.TryRunCommand(name);
            return HexText.Text(valid ? "debug_native_submitted" : "debug_native_unavailable");
        }
        catch { return HexText.Text("debug_native_uncertain"); }
    }
    internal static void Tick()
    {
        if (actor != null && (actor != Player.m_localPlayer || net != ZNet.instance || generation != CruciblePlugin.Session.Generation)) Reset();
        if (ownsBuild && actor != null && actor.m_noPlacementCost != appliedBuild) ownsBuild = false;
        if (ownsBuild && (!Tools || TimberInstalled || ZNet.instance == null || !ZNet.instance.IsServer()
            || !MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Economy).Allowed)) ReleaseBuild();
        if (!Mode || !Tools) ReleaseView();
        if (!Mode || !Tools || !MeadworksInput.Allows(InputContext.Gameplay)) return;
        // An independently enabled native debug mode already owns these physical shortcuts.
        if (NativeDebug) { ReleaseView(); return; }
        if (ZInput.GetKey(KeyCode.LeftShift) && ZInput.GetKey(KeyCode.C) && !Console.IsVisible()) { View(ZInput.GetMouseDelta()); return; }
        viewing = false;
        if (ZInput.GetKeyDown(KeyCode.Z)) Show(Execute("fly"));
        else if (ZInput.GetKeyDown(KeyCode.B)) Show(Execute("build"));
        else if (ZInput.GetKeyDown(KeyCode.K)) Show(HexWindow.DebugAction("kill"));
        else if (ZInput.GetKeyDown(KeyCode.L)) Show(HexWindow.DebugAction("drops"));
    }
    // The grant is asked once per Shift+C hold; the result stays until the next hold.
    static void View(Vector2 delta)
    {
        if (!viewing) { viewing = true; viewAllowed = Permit("view"); }
        var env = EnvMan.instance; var camera = GameCamera.instance;
        if (!viewAllowed || env == null || camera == null) return;
        HexLocalTime.ViewFraction = HexDebugView.Day(HexLocalTime.Presented(env.m_smoothDayFraction), delta.y);
        if (camera != viewCamera || camera.m_fov != appliedFov) { viewCamera = camera; savedFov = camera.m_fov; }
        appliedFov = camera.m_fov = HexDebugView.Fov(camera.m_fov, delta.x);
    }
    static void ReleaseView()
    {
        viewing = false;
        HexLocalTime.ViewFraction = -1f;
        if (viewCamera != null && savedFov > 0f && viewCamera.m_fov == appliedFov) viewCamera.m_fov = savedFov;
        viewCamera = null; savedFov = appliedFov = -1f;
    }
    // Ctrl + middle-click on the large map, as the game's own debug mode does; skipped when that mode already teleported.
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapMiddleClick))]
    static class MapTeleport
    {
        static void Postfix(Minimap __instance)
        {
            if (!Mode || !ZInput.GetKey(KeyCode.LeftControl) || NativeDebug || !Permit("teleport")) return;
            __instance.DebugTeleport(__instance.ScreenToWorldPoint(ZInput.pointerPosition));
        }
    }
    static void Show(string text)
    {
        if (text.Length > 0 && MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
    }
    static void ReleaseBuild()
    {
        var previous = actor; var restore = ownsBuild;
        ownsBuild = false;
        if (restore && previous != null && previous.m_nview != null && previous.m_nview.IsValid() && previous.m_nview.IsOwner()
            && previous.m_noPlacementCost == appliedBuild)
        {
            try { previous.SetNoPlacementCost(savedBuild); }
            catch { UnityEngine.Debug.LogWarning("Hex native build-mode restoration failed; no retry was queued."); }
        }
    }
    internal static void Reset()
    {
        try { ReleaseBuild(); ReleaseView(); }
        finally { actor = null; net = null; generation = -1; }
    }
}
