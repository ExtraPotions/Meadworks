// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using PoiProvider = System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<System.Tuple<string, float, float, float, bool>>>>;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexPoi
{
    static Task<IReadOnlyList<Tuple<string, float, float, float, bool>>>? pending;
    static CancellationTokenSource? cancel;
    static IReadOnlyList<Tuple<string, float, float, float, bool>> rows = Array.Empty<Tuple<string, float, float, float, bool>>();
    static Player? actor;
    static ZNet? net;
    static long session = -1;
    static Minimap? map;
    static RectTransform? root;
    static readonly List<(RectTransform Rect, TMP_Text Label)> markers = new();
    static bool active, estimated, renderDirty;
    static HexPoiEstimate? estimate;
    static int reportedProgress;
    static bool Allowed => Plugin.Ready && Plugin.Active && Plugin.Settings?.Enabled.Value == true
        && Plugin.Settings.MapTools.Value && MeadworksHeadless.AllowsUi && HexMovement.Owned(Player.m_localPlayer)
        && MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Information).Allowed;
    internal static void Initialize()
    {
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Map.Poi", MischiefCategory.Information);
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "Map.Estimate", MischiefCategory.Information);
    }
    static string Unavailable(string key) { Reset(); return HexText.Text(key); }
    internal static string Request()
    {
        if (!Plugin.Ready || !Plugin.Active || Plugin.Settings?.Enabled.Value != true || !Plugin.Settings.MapTools.Value
            || !MeadworksHeadless.AllowsUi || !HexMovement.Owned(Player.m_localPlayer)) return HexText.Text("map_unavailable");
        if (!MeadworksMischief.TryUse(Plugin.Guid, "Map.Poi", MischiefCategory.Information, HexText.Text("poi_actual"))) return HexText.Text("actions_denied");
        Reset(); actor = Player.m_localPlayer; net = ZNet.instance; session = CruciblePlugin.Session.Generation;
        if (net == null) return Unavailable("poi_unavailable");
        try
        {
            if (net.IsServer())
            {
                var zone = ZoneSystem.instance;
                if (zone == null || !zone.LocationsGenerated || zone.m_locationInstances == null) return Unavailable("poi_unavailable");
                rows = HexPoiRows.Select(zone.m_locationInstances.Values.Select(location => Tuple.Create(
                    location.m_location?.m_prefabName ?? "", location.m_position.x, location.m_position.y, location.m_position.z, location.m_placed)));
                active = renderDirty = true;
                return HexText.Format("poi_loaded", rows.Count);
            }
            if (!CruciblePlugin.Services.Capabilities.TryResolve<PoiProvider>(HexPoiRows.CapabilityId, HexPoiRows.Version, out var provider) || provider == null)
                return Unavailable("poi_thing_required");
            cancel = new CancellationTokenSource();
            pending = provider(cancel.Token);
            if (pending == null) { Reset(); return HexText.Text("poi_unavailable"); }
            active = true;
            return HexText.Text("poi_loading");
        }
        catch { Reset(); return HexText.Text("poi_unavailable"); }
    }
    internal static string RequestEstimate()
    {
        if (!Allowed) return HexText.Text("map_unavailable");
        if (!MeadworksMischief.TryUse(Plugin.Guid, "Map.Estimate", MischiefCategory.Information, HexText.Text("poi_estimate")))
            return HexText.Text("actions_denied");
        Reset(); actor = Player.m_localPlayer; net = ZNet.instance; session = CruciblePlugin.Session.Generation;
        try
        {
            estimate = new HexPoiEstimate(); active = estimated = true;
            return HexText.Format("poi_estimating", 0, estimate.Total);
        }
        catch { Reset(); return HexText.Text("poi_estimate_unavailable"); }
    }
    internal static string Clear() { Reset(); return HexText.Text("poi_cleared"); }
    internal static void Tick()
    {
        if (!active) return;
        if (!Allowed || actor != Player.m_localPlayer || net != ZNet.instance || session != CruciblePlugin.Session.Generation) { Reset(); return; }
        if (estimate != null)
        {
            try
            {
                if (estimate.Tick())
                {
                    rows = estimate.Read(); estimate.Dispose(); estimate = null; renderDirty = true;
                    Notify(HexText.Format("poi_estimated", rows.Count));
                }
                else if (estimate.Completed >= reportedProgress + 50)
                {
                    reportedProgress = estimate.Completed;
                    Notify(HexText.Format("poi_estimating", estimate.Completed, estimate.Total));
                }
            }
            catch { Reset(); Notify(HexText.Text("poi_estimate_unavailable")); }
        }
        if (pending?.IsCompleted == true)
        {
            string result;
            try { rows = HexPoiRows.Select(pending.GetAwaiter().GetResult()); renderDirty = true; result = HexText.Format("poi_loaded", rows.Count); }
            catch { result = HexText.Text("poi_unavailable"); Reset(); }
            pending = null; cancel?.Dispose(); cancel = null;
            // Completion is observed only here, on the native main thread after current actor/session/grant rechecks.
            if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, result);
        }
        if (map != null && map != Minimap.instance) DestroyMarkers();
        // Native UpdatePins is conditional; completed data must publish even when the already-open map stays still.
        if ((renderDirty || root == null) && Minimap.instance != null) Draw(Minimap.instance);
        if (root != null) root.gameObject.SetActive(Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large);
    }
    static void Notify(string text)
    {
        if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
    }
    static void Draw(Minimap current)
    {
        if (!active || pending != null || estimate != null || !Allowed || actor != Player.m_localPlayer || net != ZNet.instance
            || session != CruciblePlugin.Session.Generation || current != Minimap.instance || current.m_mode != Minimap.MapMode.Large
            || current.m_mapImageLarge == null || current.m_pinRootLarge == null || KitCanvas.Font == null) return;
        if (map != current || root == null) { DestroyMarkers(); map = current; root = Child(current.m_pinRootLarge, "HexPoiOverlay"); root.sizeDelta = Vector2.zero; }
        root.gameObject.SetActive(true);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; var position = new Vector3(row.Item2, row.Item3, row.Item4);
            if (markers.Count <= i) markers.Add(MakeMarker(row, i));
            var marker = markers[i]; var visible = current.IsPointVisible(position, current.m_mapImageLarge);
            marker.Rect.gameObject.SetActive(visible);
            if (!visible) continue;
            current.WorldToMapPoint(position, out var x, out var y);
            marker.Rect.anchoredPosition = current.MapPointToLocalGuiPos(x, y, current.m_mapImageLarge.uvRect, current.m_mapImageLarge.rectTransform.rect);
            var token = HexPoiRows.Token(row.Item1); var name = Localization.instance?.Localize(token) ?? row.Item1;
            if (name == token || name == "[" + token.TrimStart('$') + "]") name = row.Item1;
            var state = estimated ? "poi_unconfirmed" : !HexPoiRows.Trader(row.Item1) ? "poi_boss" : row.Item5 ? "poi_confirmed" : "poi_candidate";
            marker.Label.text = name + "\n" + HexText.Text(state);
            marker.Label.gameObject.SetActive(current.LargeZoom < current.m_showNamesZoom);
        }
        renderDirty = false;
    }
    static (RectTransform, TMP_Text) MakeMarker(Tuple<string, float, float, float, bool> row, int index)
    {
        var marker = Child(root!, "Poi" + index); marker.sizeDelta = new Vector2(18, 18);
        var image = marker.gameObject.AddComponent<Image>(); image.raycastTarget = false;
        var trader = HexPoiRows.Trader(row.Item1);
        image.sprite = KitGlyphSprites.Of(trader ? row.Item5 ? KitGlyph.Plus : KitGlyph.SmallGrid : KitGlyph.Star);
        image.color = KitUnity.Colour(!trader ? KitMapLayout.Selection : row.Item5 ? KitMapLayout.Label : KitAppearanceRules.Color(KitColorRole.Heading));
        var label = KitCanvas.Text(marker, "Label", "", 13, image.color, TextAlignmentOptions.Top, false);
        label.richText = false; label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 1); label.rectTransform.anchoredPosition = new Vector2(0, -12); label.rectTransform.sizeDelta = new Vector2(220, 38);
        return (marker, label);
    }
    static RectTransform Child(Transform parent, string name)
    {
        var node = new GameObject(name, typeof(RectTransform)); node.layer = parent.gameObject.layer;
        var rect = (RectTransform)node.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); return rect;
    }
    static void DestroyMarkers() { if (root != null) KitUnity.Retire(root.gameObject); root = null; map = null; markers.Clear(); }
    internal static void Reset()
    {
        var ownedEstimate = estimate; estimate = null;
        try { ownedEstimate?.Dispose(); } catch { /* A native iterator fault must not retain owned presentation. */ }
        estimated = renderDirty = false; reportedProgress = 0;
        var ownedCancel = cancel; cancel = null;
        try { ownedCancel?.Cancel(); } catch { /* A provider callback must not prevent releasing owned presentation. */ }
        finally { ownedCancel?.Dispose(); }
        if (pending != null) _ = pending.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        pending = null; rows = Array.Empty<Tuple<string, float, float, float, bool>>(); active = false;
        actor = null; net = null; session = -1; DestroyMarkers();
    }
    [HarmonyPatch(typeof(Minimap), "UpdatePins")]
    static class Overlay
    {
        static void Postfix(Minimap __instance) => Draw(__instance);
    }
}
