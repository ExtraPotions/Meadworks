// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. "Arrows fly to the crosshair" idea from public parity notes (behavior only); no reference source used.
using System;
using ExtraPotions.Edge.Archery;
using ExtraPotions.Edge.Config;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Bows;

/// <summary>
/// Arrows To Crosshair: after the game picks the local player's projectile spawn point and aim direction, the direction is
/// replaced by one from the spawn point to the point under the crosshair (<see cref="CrosshairAim"/>). Only the direction
/// changes; spawn point, speed, spread and gravity stay the game's. Applies to the local player's own projectile attacks,
/// including Edge secondaries that read the same spawn point. Off restores the game's direction at once.
/// </summary>
[HarmonyPatch(typeof(Attack), nameof(Attack.GetProjectileSpawnPoint))]
internal static class CrosshairAimPatch
{
    private static readonly RaycastHit[] Hits = new RaycastHit[32];
    private static int mask;
    private static bool reported;

    internal static bool Enabled => Plugin.Ready && Plugin.Settings != null
        && Plugin.Settings.Get(EdgeSettings.General.Enabled) && Plugin.Settings.Get(EdgeSettings.Ranged.ArrowsToCrosshair);

    private static int Mask => mask != 0 ? mask : mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid",
        "terrain", "vehicle", "character", "character_net", "character_ghost", "hitbox", "character_noenv");

    private static void Postfix(Attack __instance, ref Vector3 spawnPoint, ref Vector3 aimDir)
    {
        try
        {
            if (!Enabled || __instance == null) return;
            var player = __instance.m_character as Player;
            if (player == null || player != Player.m_localPlayer) return;
            var camera = GameCamera.instance != null && !GameCamera.instance.m_freeFly ? GameCamera.instance.transform : null;
            if (camera == null) return;
            var direction = Aim(player, spawnPoint, camera.position, camera.forward);
            if (direction == Vector3.zero) return;
            aimDir = direction;
            if (!reported)
            {
                reported = true;
                Plugin.Log?.Debug("Arrows To Crosshair: projectile aimed at the crosshair point.");
            }
        }
        catch (Exception error) { Plugin.Log?.Debug("Arrows To Crosshair skipped: " + error.Message); }
    }

    /// <summary>The direction from the spawn point to the first surface ahead of it on the camera's center ray.</summary>
    internal static Vector3 Aim(Player player, Vector3 spawn, Vector3 cameraPosition, Vector3 cameraForward)
    {
        var depth = CrosshairAim.SpawnDepth(N(spawn), N(cameraPosition), N(cameraForward));
        var count = Physics.RaycastNonAlloc(cameraPosition, cameraForward.normalized, Hits, CrosshairAim.FarDistance, Mask, QueryTriggerInteraction.Ignore);
        var best = CrosshairAim.FarDistance;
        for (var i = 0; i < count; i++)
        {
            var hit = Hits[i];
            if (hit.distance >= best || !CrosshairAim.Usable(hit.distance, depth) || hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<Player>() == player) continue;
            best = hit.distance;
        }
        var d = CrosshairAim.Direction(N(spawn), N(cameraPosition), N(cameraForward), best);
        return new Vector3(d.X, d.Y, d.Z);
    }

    private static System.Numerics.Vector3 N(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
}
