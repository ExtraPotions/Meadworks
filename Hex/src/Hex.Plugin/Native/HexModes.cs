// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation of the A1 modes. VAT is a behavior only design reference; no source or assemblies accessed.
// God answers the game's own InGodMode for the owned local player; Ghost answers InGhostMode for every server-confirmed player
// (HexGhost, docs/ghost-authority.md). Fly, NoClip and FreeCam borrow the
// game's own debug-flight field (with its ZDO flag), body collision detection and free-camera field only while Hex set them,
// and restore them only when they still hold Hex's value, so the console's own fly/freefly and other writers keep ownership.
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexModes
{
    private static Player? actor;
    private static bool ownsFly, ownsCollision, ownsCamera, savedCollision;

    internal static bool OwnsFly => ownsFly;
    internal static bool OwnsCollision => ownsCollision;
    internal static bool OwnsCamera => ownsCamera;

    [HarmonyPatch(typeof(Player), nameof(Player.InGodMode))]
    private static class God
    {
        private static void Postfix(Player __instance, ref bool __result) { if (!__result && HexMovement.On(__instance, "god_mode")) __result = true; }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.InGhostMode))]
    private static class Ghost
    {
        // Native AI asks the player it sees in the game that runs it. HexGhost answers the server-confirmed state for any player.
        private static void Postfix(Player __instance, ref bool __result) { if (!__result && HexGhost.Honours(__instance)) __result = true; }
    }

    /// <summary>Called every frame while Hex is active. Grants are evaluated now; a revoked or disabled mode is restored now.</summary>
    internal static void Tick()
    {
        var player = Player.m_localPlayer;
        if (actor != null && actor != player) Restore();
        var owned = HexMovement.Owned(player);
        var noclip = owned && HexMovement.On(player, "noclip");
        var fly = owned && (noclip || HexMovement.On(player, "fly"));
        var freecam = owned && HexMovement.On(player, "freecam") && GameCamera.instance != null;
        if (ownsFly && actor != null && !actor.m_debugFly) ownsFly = false; // a later writer turned native flight off
        if (fly && !ownsFly && !player.m_debugFly) { SetFly(player, true); ownsFly = true; actor = player; }
        else if (!fly && ownsFly) ReleaseFly();
        var body = player != null ? player.m_body : null;
        if (ownsCollision && actor != null && actor.m_body != null && actor.m_body.detectCollisions) ownsCollision = false;
        if (noclip && !ownsCollision && body != null && body.detectCollisions) { savedCollision = true; body.detectCollisions = false; ownsCollision = true; actor = player; }
        else if (!noclip && ownsCollision) ReleaseCollision();
        var camera = GameCamera.instance;
        if (ownsCamera && (camera == null || !camera.m_freeFly)) ownsCamera = false;
        if (freecam && !ownsCamera && !camera!.m_freeFly) { camera.m_freeFly = true; ownsCamera = true; actor ??= player; }
        else if (!freecam && ownsCamera) ReleaseCamera();
        if (!ownsFly && !ownsCollision && !ownsCamera) actor = null;
    }

    /// <summary>Panic, product shutdown, character change and logout: give every borrowed native field back.</summary>
    internal static void Restore()
    {
        ReleaseFly();
        ReleaseCollision();
        ReleaseCamera();
        actor = null;
    }

    private static void ReleaseFly()
    {
        if (ownsFly && actor != null && actor.m_debugFly) SetFly(actor, false);
        ownsFly = false;
    }

    private static void ReleaseCollision()
    {
        if (ownsCollision && actor != null && actor.m_body != null && !actor.m_body.detectCollisions) actor.m_body.detectCollisions = savedCollision;
        ownsCollision = false;
    }

    private static void ReleaseCamera()
    {
        var camera = GameCamera.instance;
        if (ownsCamera && camera != null && camera.m_freeFly) camera.m_freeFly = false;
        ownsCamera = false;
    }

    // Same field and ZDO flag the game's own ToggleDebugFly writes, without its toggle or chat message.
    private static void SetFly(Player player, bool value)
    {
        player.m_debugFly = value;
        if (player.m_nview != null && player.m_nview.IsValid() && player.m_nview.IsOwner()) player.m_nview.GetZDO().Set(ZDOVars.s_debugFly, value);
    }
}
