// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Markers tab's map (design C5, CA07a) on Crucible.UI KX09 KitMapView. The game's map image, fog
// and pin icons are read at runtime and never redistributed. The settings window that hosts it is CA07b.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Cairn.Hud;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;

namespace ExtraPotions.Cairn.Runtime
{
    /// <summary>The per-world compass marker choices of the local character (character custom data, design C4).</summary>
    public static class CairnMarkers
    {
        public static MarkerChoices Load()
        {
            var player = Player.m_localPlayer;
            var net = ZNet.instance;
            if (player == null || net == null) return MarkerChoices.Default;
            return player.m_customData.TryGetValue(MarkerChoices.DataKey(net.GetWorldUID()), out var text) ? MarkerChoices.Parse(text) : MarkerChoices.Default;
        }

        public static void Save(MarkerChoices choices)
        {
            var player = Player.m_localPlayer;
            var net = ZNet.instance;
            if (player == null || net == null || choices == null) return;
            player.m_customData[MarkerChoices.DataKey(net.GetWorldUID())] = choices.Serialize();
        }
    }

    /// <summary>
    /// The map of the Markers tab: every map pin of the player with the game's icon, bright when the compass shows it and
    /// dimmed when not. A click toggles the pin's own entry and saves the choices; a pin shown by its type stays and
    /// <see cref="OnShownByType"/> tells the tab to point at the type toggle. The tab owns focus and the pad.
    /// </summary>
    public sealed class CairnMarkerMap : IDisposable
    {
        static readonly Color Dimmed = new Color(1f, 1f, 1f, MarkerMapImage.DimmedAlpha);

        readonly KitMapView view;
        Texture2D? picture;
        Sprite? arrow;
        bool pictureFailed, framed;
        MarkerMapPlan? plan;

        /// <summary>Called after a click changed the choices (the tab's list redraws).</summary>
        public Action<MarkerChoices>? OnChanged { get; set; }

        /// <summary>Called with the pin's type when a click hit a pin shown through its type.</summary>
        public Action<string>? OnShownByType { get; set; }

        CairnMarkerMap(KitMapView view)
        {
            this.view = view;
            view.OnSelect = Clicked;
        }

        /// <summary>Null headless, before the graphical kit is active or without a game map.</summary>
        public static CairnMarkerMap? Create(RectTransform parent, Vector2 at, Vector2 size)
        {
            if (Minimap.instance == null) return null;
            var view = KitMapView.Create(parent, "CairnMarkers", at, size);
            if (view != null) KitTooltip.Set(view.Root.gameObject, CairnText.Text("ui_tab_markers"), CairnText.Text("ui_help_marker_map"));
            return view == null ? null : new CairnMarkerMap(view);
        }

        /// <summary>
        /// The map picture, composed once per window from the game's runtime map data (<see cref="MarkerMapImage"/>): the game's map
        /// texture is a flat tint per biome and its fog texture is two data channels, and the copy of the game's large-map material
        /// drawn outside its own map gave one flat brown square (suite-showcase 20261010-070300). Fog of war is kept: unexplored
        /// pixels are parchment, as on the large map.
        /// </summary>
        void BuildPicture(Minimap minimap)
        {
            if (picture != null || pictureFailed) return;
            try
            {
                int size = minimap.m_textureSize, count = size * size;
                var tints = minimap.m_mapTexture.GetPixels32();
                var own = minimap.m_explored;
                var others = minimap.m_showSharedMapData ? minimap.m_exploredOthers : null;
                if (tints.Length != count || own == null || own.Length != count) throw new InvalidOperationException("map data size");
                var heights = Heights(minimap.m_heightTexture, count);
                var water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
                var palette = MarkerMapImage.Palette(Rgb(minimap.m_meadowsColor), Rgb(minimap.m_blackforestColor), Rgb(minimap.m_swampColor),
                    Rgb(minimap.m_mountainColor), Rgb(minimap.m_heathColor), Rgb(minimap.m_mistlandsColor), Rgb(minimap.m_ashlandsColor), Rgb(minimap.m_deepnorthColor));
                var rgba = new byte[count * 4];
                MarkerMapImage.Compose(rgba, count, i => Rgb(tints[i]), i => own[i] || (others != null && others[i]),
                    heights == null ? (Func<int, float>)(_ => water + 1f) : i => heights[i], water, palette);
                picture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Cairn_MarkerMap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                picture.LoadRawTextureData(rgba);
                picture.Apply(false, true);
            }
            catch (Exception error)
            {
                pictureFailed = true;
                if (picture != null) { UnityEngine.Object.Destroy(picture); picture = null; }
                Plugin.Log?.Warning("Markers map picture unavailable, the map shows pins only: " + error.GetType().Name + ": " + error.Message);
            }
        }

        /// <summary>The height per map pixel; null when the texture is not the game's one-channel half-float height map.</summary>
        static float[]? Heights(Texture2D? texture, int count)
        {
            if (texture == null || texture.format != TextureFormat.RHalf) return null;
            var raw = texture.GetRawTextureData<ushort>();
            if (raw.Length < count) return null;
            var heights = new float[count];
            for (int i = 0; i < count; i++) heights[i] = Mathf.HalfToFloat(raw[i]);
            return heights;
        }

        /// <summary>The player arrow (HudIcons.PlayerArrow): white with a dark rim so it reads on parchment and on land.</summary>
        Sprite? Arrow()
        {
            if (arrow != null) return arrow;
            const int size = 32, rim = 2;
            var mask = HudIcons.PlayerArrow(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int edge = 0;
                    for (int dy = -rim; dy <= rim; dy++)
                        for (int dx = -rim; dx <= rim; dx++)
                        {
                            int sx = x + dx, sy = y + dy;
                            if (sx >= 0 && sy >= 0 && sx < size && sy < size) edge = Math.Max(edge, mask[sy * size + sx]);
                        }
                    float inner = mask[y * size + x] / 255f;
                    var colour = Color32.Lerp(new Color32(40, 28, 18, 255), new Color32(255, 250, 235, 255), inner);
                    colour.a = (byte)Math.Max(edge, (int)mask[y * size + x]);
                    pixels[(size - 1 - y) * size + x] = colour;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Cairn_PlayerArrow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            arrow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
            arrow.name = texture.name;
            return arrow;
        }

        static uint Rgb(Color32 c) => (uint)(c.r << 16 | c.g << 8 | c.b);

        /// <summary>Rebuilds the pins from the game's map pins and the saved choices.</summary>
        public MarkerMapPlan? Refresh()
        {
            var minimap = Minimap.instance;
            if (minimap == null) return null;
            BuildPicture(minimap);
            // Never the game's texture plainly: it holds every biome of the world, explored or not.
            view.SetMap(picture);
            Frame(minimap);
            var choices = CairnMarkers.Load();
            var source = minimap.m_pins.Where(p => p != null).ToList();
            plan = MarkerMapPlan.Build(source.Select(p => new MapPinInfo(p.m_type.ToString(), p.m_pos.x, p.m_pos.z, p.m_name)), choices);
            // Location pins (the start temple, a trader) have type None and carry their icon in m_icon, as the large map draws them.
            var icons = source.GroupBy(p => (p.m_type.ToString(), p.m_pos.x, p.m_pos.z, p.m_name ?? "")).ToDictionary(g => g.Key, g => g.First().m_icon);
            var pins = new List<KitMapPin>(plan.Pins.Count);
            float pinSize = MarkerMapImage.PinSize(minimap.m_pinSizeLarge);
            foreach (var marker in plan.Pins)
            {
                minimap.WorldToMapPoint(new Vector3(marker.Pin.X, 0f, marker.Pin.Z), out float mx, out float my);
                icons.TryGetValue((marker.Pin.Type, marker.Pin.X, marker.Pin.Z, marker.Pin.Name ?? ""), out var sprite);
                if (sprite == null && Enum.TryParse<Minimap.PinType>(marker.Pin.Type, out var type)) sprite = minimap.GetSprite(type);
                // Pin names hold game keys ("$hud_mapday 3" on a death pin); the large map localizes them the same way.
                var label = string.IsNullOrEmpty(marker.Pin.Name) ? marker.Pin.Name : global::Localization.instance?.Localize(marker.Pin.Name) ?? marker.Pin.Name;
                var pin = new KitMapPin(new Vector2(mx, my), sprite, label) { Size = pinSize };
                pin.Tint = marker.Shown == MarkerShown.Hidden ? Dimmed : Color.white;
                pins.Add(pin);
            }
            // The player's own arrow goes last: its index is past the plan's pins, so a click on it changes nothing.
            var player = Player.m_localPlayer;
            var arrowSprite = Arrow();
            if (player != null && arrowSprite != null)
            {
                minimap.WorldToMapPoint(player.transform.position, out float px, out float py);
                var facing = player.transform.forward;
                var me = new KitMapPin(new Vector2(px, py), arrowSprite, "", alwaysShow: true) { Size = MarkerMapImage.ArrowSize };
                me.Rotation = MarkerMapImage.PlayerRotation(ExtraPotions.Cairn.Hud.Compass.Heading(facing.x, facing.z));
                pins.Add(me);
            }
            view.SetPins(pins);
            return plan;
        }

        /// <summary>Once per window: opens on the player at the large map's closeness (<see cref="MarkerMapImage.StartView"/>).</summary>
        void Frame(Minimap minimap)
        {
            if (framed) return;
            framed = true;
            var player = Player.m_localPlayer;
            float x = float.NaN, y = float.NaN;
            if (player != null) minimap.WorldToMapPoint(player.transform.position, out x, out y);
            var start = MarkerMapImage.StartView(x, y);
            view.Camera.SetZoom(start.Zoom);
            view.Camera.CenterOn(new KitVector(start.X, start.Y));
        }

        /// <summary>One pad frame from the tab while the map has focus.</summary>
        public void Pad(Vector2 stick, float zoomAxis, bool select, float deltaTime) => view.Pad(stick, zoomAxis, select, deltaTime);

        public void Dispose()
        {
            OnChanged = null;
            OnShownByType = null;
            plan = null;
            view.Dispose();
            if (picture != null) { UnityEngine.Object.Destroy(picture); picture = null; }
            if (arrow != null) { UnityEngine.Object.Destroy(arrow.texture); UnityEngine.Object.Destroy(arrow); arrow = null; }
        }

        void Clicked(int index)
        {
            if (plan == null || index < 0 || index >= plan.Pins.Count) return;
            var result = plan.Toggle(index, CairnMarkers.Load());
            if (result.ShownByType)
            {
                OnShownByType?.Invoke(plan.Pins[index].Pin.Type);
                return;
            }
            if (!result.Changed) return;
            CairnMarkers.Save(result.Choices);
            Refresh();
            OnChanged?.Invoke(result.Choices);
        }
    }
}
