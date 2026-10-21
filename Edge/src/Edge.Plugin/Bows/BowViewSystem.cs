// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Bow zoom and draw feedback ideas seen in BetterArchery (ishid4); implemented from its public feature
// list and the game's own GameCamera/Hud code only.
using System;
using ExtraPotions.Edge.Archery;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime.Bows;

/// <summary>
/// Client-side bow visuals: the camera's field of view narrows with the local player's bow draw (<see cref="BowZoom"/>),
/// and a kit meter under the crosshair fills with the draw. Neither touches the game's own FOV state: the zoom rewrites the
/// cameras after the game set them each frame, so turning it off restores the game's view at once. Transitions (zoom in,
/// full zoom, zoom out, full draw) are logged at Verbose; nothing is logged per frame.
/// </summary>
internal static class BowViewSystem
{
    private static float blend;
    private static bool zoomLogged, fullZoomLogged;
    private static KitMeter? meter;
    private static Transform? meterParent;
    private static bool fullDrawLogged;
    private static bool reported;

    private static float DrawFraction(Player? player)
    {
        if (player == null || player.IsDead() || !player.IsDrawingBow()) return 0f;
        var draw = player.GetAttackDrawPercentage();
        return float.IsNaN(draw) ? 0f : Mathf.Clamp01(draw);
    }

    private static bool Active(EdgeSetting<bool> toggle) =>
        Plugin.Settings != null && EdgeModules.Active(EdgeModule.Archery) && Plugin.Settings.Get(toggle);

    internal static void ApplyZoom(GameCamera camera, float dt)
    {
        if (camera.m_freeFly || camera.m_camera == null) { blend = 0f; return; }
        var target = Active(EdgeSettings.Interface.BowZoom) ? DrawFraction(Player.m_localPlayer) : 0f;
        blend = BowZoom.Step(blend, target, dt, BowZoom.EaseSeconds);
        var factor = Plugin.Settings?.Get(EdgeSettings.Interface.BowZoomFactor) ?? 1f;
        if (blend <= 0f)
        {
            if (zoomLogged) Plugin.Log?.Verbose("Bow zoom out: fov " + camera.m_fov.ToString("0.0") + ".");
            zoomLogged = fullZoomLogged = false;
            return;
        }
        var fov = BowZoom.Fov(camera.m_fov, factor, blend);
        camera.m_camera.fieldOfView = fov;
        if (camera.m_skyCamera != null) camera.m_skyCamera.fieldOfView = fov;
        if (!zoomLogged) { zoomLogged = true; Plugin.Log?.Verbose("Bow zoom in: base fov " + camera.m_fov.ToString("0.0") + ", factor " + factor.ToString("0.00") + "."); }
        if (blend >= 1f && !fullZoomLogged) { fullZoomLogged = true; Plugin.Log?.Verbose("Bow zoom full: fov " + fov.ToString("0.0") + "."); }
    }

    /// <summary>Called from the plugin's Update.</summary>
    internal static void UpdateIndicator(Player? player)
    {
        try
        {
            var crosshair = Hud.instance?.m_crosshair;
            var draw = Plugin.AllowsUi && player != null && player == Player.m_localPlayer && Active(EdgeSettings.Interface.DrawIndicator) ? DrawFraction(player) : 0f;
            if (draw <= 0f || crosshair == null || Hud.IsUserHidden())
            {
                if (meter?.Root != null) meter.Root.gameObject.SetActive(false);
                fullDrawLogged = false;
                return;
            }
            var parent = crosshair.rectTransform;
            if (meter?.Root == null || meterParent != parent)
            {
                DisposeIndicator();
                meter = KitMeter.Create(parent!, "Edge_DrawIndicator", Vector2.zero, new Vector2(64, 8));
                if (meter == null) return;
                meterParent = parent;
                var rect = meter.Root;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0, -34);
                meter.Label.gameObject.SetActive(false);
                Plugin.Log?.Verbose("Draw indicator created under the crosshair.");
            }
            meter.Root.gameObject.SetActive(true);
            var full = draw >= 1f;
            meter.Set(draw, 1f, "", full ? KitAppearance.Color(KitColorRole.OnPrimary) : (Color?)null);
            if (full && !fullDrawLogged) { fullDrawLogged = true; Plugin.Log?.Verbose("Draw indicator: full draw."); }
        }
        catch (Exception error)
        {
            if (reported) return;
            reported = true;
            Plugin.Log?.Warning("Draw indicator failed and is hidden. " + error.GetType().Name + ": " + error.Message);
            DisposeIndicator();
        }
    }

    internal static void DisposeIndicator()
    {
        meter?.Dispose();
        meter = null;
        meterParent = null;
        fullDrawLogged = false;
    }

    internal static void Reset()
    {
        DisposeIndicator();
        blend = 0f;
        zoomLogged = fullZoomLogged = false;
    }
}

[HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
internal static class BowZoomCameraPatch
{
    private static bool reported;

    private static void Postfix(GameCamera __instance, float dt)
    {
        try { BowViewSystem.ApplyZoom(__instance, dt); }
        catch (Exception error)
        {
            if (reported) return;
            reported = true;
            Plugin.Log?.Warning("Bow zoom failed; the camera stays as the game sets it. " + error.GetType().Name + ": " + error.Message);
        }
    }
}
