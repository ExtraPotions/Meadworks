// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2022 Yohann MARTIN, MIT. ValheimTooler/Core/ESP.cs at
// 2c4777393cb759c3501de5216e8e8dd82b9f7bdf: periodic typed collection, local-player exclusion,
// screen projection and four-edge character box. Retained notice: ValheimTooler-MIT.txt.
// Changed by expDARE: current Information/owner/session gates, bounded pool, native registries,
// literal kit labels/code glyphs, component resource recognition, dungeons and multilingual filtering.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexEsp
{
    const int MaxVisible = 256, MaxCandidates = 8192;
    sealed class Target
    {
        internal Component Source = null!;
        internal string Kind = "", Name = "";
        internal float Distance;
    }
    sealed class Marker
    {
        internal RectTransform Root = null!;
        internal TMP_Text Label = null!;
        internal Image Glyph = null!;
        internal readonly List<Image> Edges = new();
    }
    static readonly List<Target> targets = new();
    static readonly HashSet<int> seen = new();
    static readonly List<Marker> markers = new();
    static RectTransform? root;
    static Player? actor;
    static ZNet? net;
    static long session = -1;
    static float refreshAt;
    static string shape = "";
    static bool Allowed => Plugin.Ready && Plugin.Active && MeadworksHeadless.AllowsUi
        && HexMovement.Owned(Player.m_localPlayer) && MeadworksAuthority.ProductActive(Plugin.Guid, out _)
        && Plugin.Settings?.Effective("esp", false) == true;

    internal static void Reset()
    {
        if (root != null) { root.gameObject.SetActive(false); Object.Destroy(root.gameObject); }
        root = null; targets.Clear(); seen.Clear(); markers.Clear(); actor = null; net = null; session = -1; shape = ""; refreshAt = 0;
    }
    internal static void Tick()
    {
        if (!Allowed) { if (actor != null || root != null || targets.Count > 0) Reset(); return; }
        if (actor != Player.m_localPlayer || net != ZNet.instance || session != CruciblePlugin.Session.Generation) Reset();
        actor = Player.m_localPlayer; net = ZNet.instance; session = CruciblePlugin.Session.Generation;
        var camera = global::Utils.GetMainCamera();
        var parent = KitCanvas.Front as RectTransform;
        if (camera == null || parent == null || KitCanvas.Font == null)
        { if (root != null) root.gameObject.SetActive(false); return; }
        if (root != null && root.parent != parent)
        { Reset(); actor = Player.m_localPlayer; net = ZNet.instance; session = CruciblePlugin.Session.Generation; }
        // A display setting or language change rebuilds the search text immediately; ordinary scans keep the reference's cadence.
        var settings = Plugin.Settings!;
        var currentShape = settings.EspDistance.Value + ":" + Bound(settings.EspSearch.Value) + ":" + MeadworksBlockedOptions.Language;
        foreach (var kind in settings.EspKinds) currentShape += ":" + kind.Value.Value;
        if (Time.unscaledTime >= refreshAt || currentShape != shape)
        {
            shape = currentShape; refreshAt = Time.unscaledTime + 1.5f;
            try { Collect(camera); }
            catch { targets.Clear(); } // Native/external scene faults never interfere with the player's update.
        }
        if (root == null)
        {
            root = Child(parent, "Meadworks.Hex.ESP");
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetAsFirstSibling(); // Product windows remain above their informational overlay.
        }
        root.gameObject.SetActive(true);
        var canvas = root.GetComponentInParent<Canvas>();
        var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var count = 0;
        foreach (var target in targets)
        {
            try
            {
                if (!Live(target.Source) || !settings.EspKinds[target.Kind].Value) continue;
                var world = target.Source.transform.position;
                var distance = Vector3.Distance(camera.transform.position, world);
                if (!Finite(distance) || distance > settings.EspDistance.Value || distance < 2f) continue;
                var point = camera.WorldToScreenPoint(world);
                if (!Finite(point.x) || !Finite(point.y) || point.z <= 0f || point.x < 0f || point.x > Screen.width || point.y < 0f || point.y > Screen.height) continue;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, point, uiCamera, out var local)) continue;
                if (count == markers.Count) markers.Add(MakeMarker());
                var marker = markers[count++]; marker.Root.gameObject.SetActive(true); marker.Root.anchoredPosition = local;
                var color = Colour(target); marker.Label.color = marker.Glyph.color = color;
                marker.Label.text = target.Name + " [" + (int)distance + " m]";
                marker.Glyph.sprite = KitGlyphSprites.Of(target.Kind == "dungeons" ? KitGlyph.SmallGrid : target.Kind == "resources" ? KitGlyph.Plus : KitGlyph.Star);
                var character = target.Source as Character;
                var height = 0f;
                if (character != null)
                {
                    var eye = camera.WorldToScreenPoint(character.GetEyePoint());
                    if (eye.z > 0f && Finite(eye.y) && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eye, uiCamera, out var top))
                        height = Mathf.Clamp(Mathf.Abs(top.y - local.y), 4f, 500f);
                }
                Box(marker, height * 0.65f, height, color);
                if (count >= MaxVisible) break;
            }
            catch { /* One destroyed or custom target cannot hide the remaining targets. */ }
        }
        for (var i = count; i < markers.Count; i++) markers[i].Root.gameObject.SetActive(false);
    }
    static string Bound(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var text = value!.Substring(0, Math.Min(256, value.Length)).ToCharArray();
        for (var i = 0; i < text.Length; i++) if (char.IsControl(text[i])) text[i] = ' ';
        return new string(text);
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Live(Component source)
    {
        if (source == null || !source.gameObject.activeInHierarchy) return false;
        if (source is Character character) return character != Player.m_localPlayer && !character.IsDead();
        if (source is Pickable pickable) return !pickable.m_picked && pickable.GetEnabled != 0;
        if (source is PickableItem item) return !item.m_picked;
        if (source is ItemDrop drop) return drop.m_nview != null && drop.m_nview.IsValid() && drop.m_itemData.m_stack > 0;
        return true;
    }
    static void Collect(Camera camera)
    {
        targets.Clear(); seen.Clear();
        var settings = Plugin.Settings!;
        var query = HexSpawnData.Fold(Bound(settings.EspSearch.Value));
        void Add(Component source, string kind, Func<string> nativeName)
        {
            try
            {
                if (targets.Count >= MaxCandidates || !settings.EspKinds[kind].Value || !Live(source)) return;
                var distance = Vector3.Distance(camera.transform.position, source.transform.position);
                if (!Finite(distance) || distance < 2f || distance > settings.EspDistance.Value) return;
                var token = Bound(nativeName());
                var identity = Bound(global::Utils.GetPrefabName(source.gameObject));
                var name = Bound(Localization.instance?.Localize(token) ?? token);
                if (string.IsNullOrWhiteSpace(name)) name = identity;
                var search = HexSpawnData.Fold(name + " " + token + " " + identity);
                if (query.Length > 0 && search.IndexOf(query, StringComparison.Ordinal) < 0) return;
                if (!seen.Add(source.gameObject.GetInstanceID())) return;
                targets.Add(new Target { Source = source, Kind = kind, Name = name, Distance = distance });
            }
            catch { /* Malformed external prefab/name data is isolated per target. */ }
        }
        foreach (var character in Character.GetAllCharacters())
            if (character != null) Add(character, character.IsPlayer() ? "players" : "creatures", () => character is Player player ? player.GetPlayerName() : character.m_name);
        foreach (var drop in ItemDrop.s_instances) Add(drop, "drops", () => drop.m_itemData.m_shared.m_name);
        if (settings.EspKinds["pickables"].Value)
        {
            foreach (var value in Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None)) Add(value, "pickables", value.GetHoverName);
            foreach (var value in Object.FindObjectsByType<PickableItem>(FindObjectsSortMode.None)) Add(value, "pickables", value.GetHoverName);
        }
        if (settings.EspKinds["resources"].Value)
        {
            foreach (var value in Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None)) Add(value, "resources", () => value.m_name);
            foreach (var value in Object.FindObjectsByType<MineRock>(FindObjectsSortMode.None)) Add(value, "resources", () => value.m_name);
            foreach (var value in Object.FindObjectsByType<TreeBase>(FindObjectsSortMode.None)) Add(value, "resources", () => global::Utils.GetPrefabName(value.gameObject));
            foreach (var value in Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None))
            {
                // Component identity recognizes resource-bearing breakables in every game language; placed pieces are excluded.
                var hover = value.GetComponent<HoverText>();
                if (hover != null && value.GetComponent<DropOnDestroyed>() != null && value.GetComponent<Piece>() == null)
                    Add(value, "resources", () => hover.m_text);
            }
        }
        if (settings.EspKinds["dungeons"].Value) foreach (var location in Location.s_allLocations)
            if (location != null && (location.m_hasInterior || location.m_generator != null))
                Add(location, "dungeons", () => location.m_discoverLabel);
        targets.Sort((left, right) => left.Distance.CompareTo(right.Distance));
        if (targets.Count > MaxVisible) targets.RemoveRange(MaxVisible, targets.Count - MaxVisible);
    }
    static Color Colour(Target target) => KitUnity.Colour(target.Kind == "players" ? KitMapLayout.Selection
        : target.Kind == "creatures" ? target.Source is Character character && character.IsTamed() ? KitAppearanceRules.Color(KitColorRole.Heading) : KitAppearanceRules.Color(KitColorRole.Accent)
        : target.Kind == "dungeons" ? KitMapLayout.Label : KitAppearanceRules.Color(KitColorRole.Text));
    static RectTransform Child(Transform parent, string name)
    {
        var node = new GameObject(name, typeof(RectTransform)); node.layer = parent.gameObject.layer;
        var rect = (RectTransform)node.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); return rect;
    }
    static Marker MakeMarker()
    {
        var marker = new Marker { Root = Child(root!, "Target") };
        marker.Root.sizeDelta = Vector2.zero;
        var glyph = Child(marker.Root, "Glyph"); glyph.sizeDelta = new Vector2(8, 8);
        marker.Glyph = glyph.gameObject.AddComponent<Image>(); marker.Glyph.raycastTarget = false;
        marker.Label = KitCanvas.Text(marker.Root, "Label", "", 13, Color.white, TextAlignmentOptions.BottomLeft, false);
        marker.Label.richText = false; marker.Label.rectTransform.pivot = new Vector2(0, 0);
        marker.Label.rectTransform.anchoredPosition = new Vector2(8, 4); marker.Label.rectTransform.sizeDelta = new Vector2(300, 24);
        for (var i = 0; i < 4; i++)
        {
            var edge = Child(marker.Root, "Edge" + i); edge.pivot = Vector2.zero;
            var image = edge.gameObject.AddComponent<Image>(); image.raycastTarget = false; marker.Edges.Add(image);
        }
        return marker;
    }
    static void Box(Marker marker, float width, float height, Color color)
    {
        for (var i = 0; i < 4; i++)
        {
            var edge = marker.Edges[i]; edge.gameObject.SetActive(height > 0f); edge.color = color;
            edge.rectTransform.anchoredPosition = new Vector2(-width / 2f + (i == 1 ? width - 1f : i >= 2 ? 1f : 0f), i == 3 ? height - 1f : 0f);
            edge.rectTransform.sizeDelta = i < 2 ? new Vector2(1, height) : new Vector2(Mathf.Max(0, width - 2f), 1);
        }
    }
}
