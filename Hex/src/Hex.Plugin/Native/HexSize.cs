// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexSize
{
    private static readonly HashSet<ZSyncTransform> enabledSync = new();
    private static Player? actor;
    private static Vector3 baseline, applied;
    private static long generation;

    internal static void Initialize()
    {
        foreach (var player in Player.GetAllPlayers()) EnableSync(player.GetComponent<ZSyncTransform>());
    }
    private static void EnableSync(ZSyncTransform sync)
    {
        // Every observer uses the game's existing owner-scale replication, regardless of its own size preference.
        if (!Plugin.Active || sync == null || sync.GetComponent<Player>() == null || sync.m_syncScale) return;
        sync.m_syncScale = true; enabledSync.Add(sync);
    }
    private static bool NetworkOwner(Player player) => player != null && player.m_nview != null
        && player.m_nview.IsValid() && player.m_nview.IsOwner();
    private static bool Finite(Vector3 value) => !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)
        || float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
    private static bool Eligible(Player player) => HexMovement.Owned(player) && !player.InBed() && !player.IsRiding()
        && !player.IsAttachedToShip() && !player.IsTeleporting() && !player.InIntro() && !player.InCutscene()
        && player.GetHealth() > 0f;

    internal static void Tick()
    {
        var player = Player.m_localPlayer;
        if (actor != null && (actor != player || generation != CruciblePlugin.Session.Generation)) Release();
        var factor = HexCatalog.SizeFactor(Plugin.Settings?.Effective("player_size", 100) ?? 100);
        var sync = player != null ? player.GetComponent<ZSyncTransform>() : null;
        if (factor == 1f || !Eligible(player) || sync == null || !sync.m_syncScale) { Release(); return; }
        var current = player.transform.localScale;
        var publish = actor == null || !current.Equals(applied);
        if (!Finite(current)) { Release(); return; }
        if (actor == null)
        {
            actor = player; generation = CruciblePlugin.Session.Generation; baseline = current; applied = current;
        }
        else if (!current.Equals(applied)) baseline = current; // an external transform writer establishes the new baseline
        var desired = baseline * factor;
        if (!Finite(desired)) { Release(); return; }
        publish |= !desired.Equals(applied);
        if (!current.Equals(desired)) player.transform.localScale = desired;
        applied = desired;
        var zdo = player.m_nview.GetZDO();
        if (publish) zdo.Set(ZDOVars.s_scaleHash, applied);
    }

    internal static void Release()
    {
        var player = actor;
        actor = null;
        // Once ownership is lost only native ClientSync/new owner may decide this network object's scale.
        if (!NetworkOwner(player)) return;
        var current = player.transform.localScale;
        if (!Finite(current)) return;
        if (current.Equals(applied)) { current = baseline; player.transform.localScale = current; }
        var zdo = player.m_nview.GetZDO();
        // Publish baseline before releasing the bridge, but never overwrite a later different ZDO writer.
        if (zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero).Equals(applied)) zdo.Set(ZDOVars.s_scaleHash, current);
    }
    internal static void Shutdown()
    {
        Release();
        foreach (var sync in enabledSync) if (sync != null && sync.m_syncScale) sync.m_syncScale = false;
        enabledSync.Clear();
    }

    [HarmonyPatch]
    private static class PlayerSync
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(ZSyncTransform), "Awake");
            yield return AccessTools.DeclaredMethod(typeof(ZSyncTransform), "OnEnable");
        }
        private static void Postfix(ZSyncTransform __instance) => EnableSync(__instance);
    }
    [HarmonyPatch(typeof(ZSyncTransform), "OnDisable")]
    private static class RemoveSync
    {
        private static void Prefix(ZSyncTransform __instance)
        {
            if (actor != null && actor.GetComponent<ZSyncTransform>() == __instance) Release();
            if (enabledSync.Remove(__instance) && __instance.m_syncScale) __instance.m_syncScale = false;
        }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.AttachStart))]
    private static class Attach
    {
        private static void Prefix(Player __instance, bool isBed, bool onShip)
        {
            if (actor == __instance && (isBed || onShip || __instance.IsRiding())) Release();
        }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    private static class Death
    {
        private static void Prefix(Player __instance) { if (actor == __instance) Release(); }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
    private static class Teleport
    {
        private static void Prefix(Player __instance) { if (actor == __instance) Release(); }
    }
}
