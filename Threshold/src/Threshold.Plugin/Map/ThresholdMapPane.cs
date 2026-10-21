// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the picker's map pane (design section 10, TH06) on Crucible.UI KX09 KitMapView. The game's map
// image, fog, portal icon and player arrow are read at runtime and never redistributed.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Map;
using ExtraPotions.Threshold.Portals;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Threshold.Runtime.Map
{
    /// <summary>
    /// One map pane inside the picker window. The window owns focus and the pad (it calls <see cref="Pad"/> only while the
    /// map has focus and nothing is being typed); this pane turns a pin click into the client's destination (U1, U3).
    /// </summary>
    public sealed class ThresholdMapPane : IDisposable
    {
        const string PortalPrefab = "portal_wood";

        // Private game members, read through Harmony's accessors (names verified by the hook table tests).
        delegate void WorldToMap(Minimap minimap, Vector3 point, out float mx, out float my);
        static readonly AccessTools.FieldRef<Minimap, Texture2D> FogTexture = AccessTools.FieldRefAccess<Minimap, Texture2D>("m_fogTexture");
        static readonly WorldToMap WorldToMapPoint = AccessTools.MethodDelegate<WorldToMap>(
            AccessTools.Method(typeof(Minimap), "WorldToMapPoint", new[] { typeof(Vector3), typeof(float).MakeByRefType(), typeof(float).MakeByRefType() }));
        static readonly Func<Minimap, Vector3, bool> IsExploredAt = AccessTools.MethodDelegate<Func<Minimap, Vector3, bool>>(
            AccessTools.Method(typeof(Minimap), "IsExplored", new[] { typeof(Vector3) }));

        readonly KitMapView view;
        Material? gameMap;
        MapPinPlan? plan;
        readonly Dictionary<KitVector, bool> explored = new Dictionary<KitVector, bool>();

        /// <summary>Called with the clicked destination; the picker travels to it (U1).</summary>
        public Action<PortalEntry>? OnPick { get; set; }

        /// <summary>The hub of the map's Return To Hub button, or null for no button (every portal opens the map;
        /// the window shows the button with <c>picker_return_to_hub</c> while this is set).</summary>
        public PortalEntry? ReturnHub => plan?.ReturnHub;

        /// <summary>Called after a hub pin's "Set as home hub" or "Clear home hub" changed the home hub (null when
        /// cleared); the window rebuilds the plan so the marker and the Return To Hub target follow (Threshold follow-up 2).</summary>
        public Action<PortalEntry?>? OnHomeChanged { get; set; }

        ThresholdMapPane(KitMapView view)
        {
            this.view = view;
            view.OnSelect = Picked;
        }

        /// <summary>Null headless, before the graphical kit is active or without a game map.</summary>
        public static ThresholdMapPane? Create(RectTransform parent, Vector2 at, Vector2 size)
        {
            if (Minimap.instance == null) return null;
            try
            {
                if (FogTexture == null || WorldToMapPoint == null || IsExploredAt == null) return null;
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("Threshold: the game's map members changed; no map pane: " + e.GetType().Name + ": " + e.Message);
                return null;
            }
            var view = KitMapView.Create(parent, "ThresholdMap", at, size);
            return view == null ? null : new ThresholdMapPane(view);
        }

        void UseGameMap(Minimap minimap)
        {
            if (gameMap == null)
            {
                var source = minimap.m_mapImageLarge != null ? minimap.m_mapImageLarge.material : null;
                if (source == null) return;
                gameMap = new Material(source) { name = "Threshold_MapPane" };
                if (gameMap.HasProperty("_zoom")) gameMap.SetFloat("_zoom", 1f);
                if (gameMap.HasProperty("_pixelSize")) gameMap.SetFloat("_pixelSize", 200f);
                if (gameMap.HasProperty("_mapCenter")) gameMap.SetVector("_mapCenter", Vector4.zero);
            }
            view.SetMapMaterial(gameMap);
        }

        /// <summary>Shows the plan's pins over the map image the settings choose, zoomed and centered on this portal.</summary>
        public void Show(MapPinPlan next)
        {
            plan = next ?? throw new ArgumentNullException(nameof(next));
            var settings = Plugin.Settings;
            var minimap = Minimap.instance;
            if (settings == null || minimap == null) return;
            var image = MapPaneRules.Image(settings.Get(ThresholdSettings.Map.UseGameMap), settings.Get(ThresholdSettings.Map.HideUnexploredMap));
            if (MapPaneRules.UsesGameMaterial(image))
            {
                // The map texture is encoded data; a plain draw is one flat color. Draw it with a copy of the game's large-map
                // material (the game's own is never changed); its shader applies terrain and fog.
                UseGameMap(minimap);
                view.SetMap(minimap.m_mapTexture, gameMap != null ? null : FogTexture(minimap));
            }
            else
            {
                view.SetMapMaterial(null);
                view.SetMap(null, image == MapImage.Plain ? null : FogTexture(minimap));
            }

            var portalIcon = PortalIcon();
            var playerIcon = minimap.m_largeMarker != null ? minimap.m_largeMarker.GetComponent<Image>()?.sprite : null;
            var untagged = ThresholdText.Text(ThresholdTextKeys.PickerUntagged);
            var pins = new List<KitMapPin>(next.Pins.Count);
            explored.Clear();
            foreach (var spec in next.Pins)
            {
                var at = spec.Portal != null ? ToMap(minimap, spec.Portal.Position) : PlayerMapPoint(minimap, out _);
                var label = spec.Kind == MapPinKind.Player ? "" : spec.Label.Length > 0 ? spec.Label : untagged;
                var pin = new KitMapPin(at, spec.Kind == MapPinKind.Player ? playerIcon : portalIcon, label, spec.AlwaysShow);
                var tint = MapPinColours.ForPin(spec, pins.Count == next.ReturnHubIndex);
                pin.Tint = new Color(tint.R, tint.G, tint.B, tint.A);
                if (spec.IsHome) pin.Label = label + " (" + ThresholdText.Text(ThresholdTextKeys.PickerHomeHub) + ")";
                pins.Add(pin);
                if (spec.Portal != null) explored[KitUnity.Vector(at)] = IsExploredAt(minimap, Position(spec.Portal.Position));
            }
            view.SetPins(pins);
            view.SetExplored(MapPaneRules.UsesExploredMask(settings.Get(ThresholdSettings.General.OnlyExploredPortals)) ? IsExplored : null);
            view.ShowLabels = settings.Get(ThresholdSettings.Map.ShowMapLabels);
            view.Camera.SetZoom(MapPaneRules.StartZoom(settings.Get(ThresholdSettings.Map.StartZoom)));
            int centre = next.HereIndex >= 0 ? next.HereIndex : next.PlayerIndex;
            view.Camera.CenterOn(KitUnity.Vector(pins[centre].At));
            view.Select(-1);
            Tick();
        }

        /// <summary>Each frame while shown: moves the player arrow.</summary>
        public void Tick()
        {
            var minimap = Minimap.instance;
            if (plan == null || minimap == null) return;
            var at = PlayerMapPoint(minimap, out float rotation);
            view.MovePin(plan.PlayerIndex, at, rotation);
        }

        /// <summary>Shows a list selection on the map (no travel).</summary>
        public void SelectPortal(PortalId id, bool centre)
        {
            if (plan != null) view.Select(plan.IndexOf(id), centre);
        }

        /// <summary>The window's Return To Hub button: travels to <see cref="ReturnHub"/> like a pin click (no-op without one).</summary>
        public void ReturnToHub()
        {
            var hub = plan?.ReturnHub;
            if (hub == null) return;
            try { OnPick?.Invoke(hub); }
            catch (Exception e) { Plugin.Log?.Error("Threshold: Return To Hub failed: " + e); }
        }

        /// <summary>The hub pin action the window offers on pin <paramref name="index"/>: <c>picker_set_home</c>,
        /// <c>picker_clear_home</c> on the current home hub, or null (not a hub destination pin).</summary>
        public string? HomeActionKey(int index)
        {
            if (plan?.HomeCandidateAt(index) == null) return null;
            return plan.Pins[index].IsHome ? ThresholdTextKeys.PickerClearHome : ThresholdTextKeys.PickerSetHome;
        }

        /// <summary>Runs the hub pin action on pin <paramref name="index"/>: sets that hub as home in this world, or
        /// clears it when it already is the home hub. No-op on any other pin.</summary>
        public void ToggleHomeAt(int index)
        {
            var hub = plan?.HomeCandidateAt(index);
            if (hub == null) return;
            try
            {
                if (plan!.Pins[index].IsHome)
                {
                    ThresholdHomeHub.Clear();
                    OnHomeChanged?.Invoke(null);
                }
                else if (ThresholdHomeHub.Save(hub))
                {
                    OnHomeChanged?.Invoke(hub);
                }
            }
            catch (Exception e) { Plugin.Log?.Error("Threshold: home hub change failed: " + e); }
        }

        /// <summary>One pad frame from the window while the map has focus.</summary>
        public void Pad(Vector2 stick, float zoomAxis, bool select, float deltaTime) => view.Pad(stick, zoomAxis, select, deltaTime);

        public void Dispose()
        {
            OnPick = null;
            plan = null;
            view.Dispose();
            if (gameMap != null) { UnityEngine.Object.Destroy(gameMap); gameMap = null; }
        }

        void Picked(int index)
        {
            var target = plan?.DestinationAt(index);
            if (target == null) return;
            try { OnPick?.Invoke(target); }
            catch (Exception e) { Plugin.Log?.Error("Threshold: map pick failed: " + e); }
        }

        bool IsExplored(KitVector at) => explored.TryGetValue(at, out var seen) && seen;

        /// <summary>Whether the local player explored the map at a portal (Only Explored Portals, A7); true without a map.</summary>
        public static bool Explored(MapPoint at)
        {
            var minimap = Minimap.instance;
            return minimap == null || IsExploredAt == null || IsExploredAt(minimap, Position(at));
        }

        static Vector3 Position(MapPoint p) => new Vector3(p.X, p.Y, p.Z);

        static Vector2 ToMap(Minimap minimap, MapPoint p)
        {
            WorldToMapPoint(minimap, Position(p), out float mx, out float my);
            return new Vector2(mx, my);
        }

        static Vector2 PlayerMapPoint(Minimap minimap, out float rotation)
        {
            rotation = 0f;
            var player = Player.m_localPlayer;
            if (player == null) return new Vector2(0.5f, 0.5f);
            rotation = -player.transform.rotation.eulerAngles.y;
            WorldToMapPoint(minimap, player.transform.position, out float mx, out float my);
            return new Vector2(mx, my);
        }

        static Sprite? PortalIcon()
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(PortalPrefab) : null;
            return prefab != null ? prefab.GetComponent<Piece>()?.m_icon : null;
        }
    }
}
