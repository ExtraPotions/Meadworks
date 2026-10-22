// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Client colors on the game's own materials. Originals are put back on toggle-off and unload.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Config;
using ExtraPotions.Solstice.Effects;
using ExtraPotions.Solstice.Visuals;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Seasonal color for this client. Materials are scanned when the season, the snow step, the freeze flag or a visual
    /// toggle changes, not every frame. <c>_Color</c> multiplies the texture, so Snow Cover does not push it toward white.
    /// Ground snow and frosted grass are SeasonWinterLook. This class still raises <c>_SnowLevel</c> where
    /// that property exists. A missing property is logged once and skipped.
    /// </summary>
    public static class SeasonVisuals
    {
        struct Kept
        {
            public Material Material;
            public VisualTarget Target;
            public Color Color;
            public float Snow;
            public Color Shallow;
            public Color Top;
            public Color Foam;
            public float Ripple;
        }

        readonly struct Stamp : IEquatable<Stamp>
        {
            public Stamp(int season, int snowMilli, bool frozen, bool world, bool snow, bool water, bool map)
            {
                Season = season;
                SnowMilli = snowMilli;
                Frozen = frozen;
                World = world;
                Snow = snow;
                Water = water;
                Map = map;
            }

            public int Season { get; }
            public int SnowMilli { get; }
            public bool Frozen { get; }
            public bool World { get; }
            public bool Snow { get; }
            public bool Water { get; }
            public bool Map { get; }

            public bool Equals(Stamp other) =>
                Season == other.Season && SnowMilli == other.SnowMilli && Frozen == other.Frozen
                && World == other.World && Snow == other.Snow && Water == other.Water && Map == other.Map;

            public override bool Equals(object obj) => obj is Stamp other && Equals(other);
            public override int GetHashCode() => Season;
        }

        const int WorldColor = (int)(VisualTarget.Grass | VisualTarget.Leaf | VisualTarget.Terrain);
        const int ColorMask = WorldColor | (int)VisualTarget.WaterSurface;
        const int WaterMask = (int)(VisualTarget.WaterSurface | VisualTarget.ShallowWater | VisualTarget.WaterTop | VisualTarget.WaterFoam | VisualTarget.WaterRipple);

        static readonly Dictionary<int, Kept> cache = new Dictionary<int, Kept>();
        static readonly HashSet<int> seen = new HashSet<int>();
        static readonly List<int> drop = new List<int>();

        static MeadworksLog log;
        static int colorId, snowId, shallowId, topId, foamId, rippleId;
        static bool ready, dirty = true, applied, mapReady, warned;
        static bool loggedGrass, loggedTerrain, loggedSnow, loggedWater, loggedMap;
        static Stamp drawn;
        static Color smallOriginal, largeOriginal;
        static bool smallStored, largeStored;
        static RawImage smallImage, largeImage;

        public static void Initialize(MeadworksLog logger)
        {
            log = logger;
            if (!MeadworksHeadless.AllowsUi) return;
            colorId = Shader.PropertyToID(SeasonShader.Color);
            snowId = Shader.PropertyToID(SeasonShader.SnowLevel);
            shallowId = Shader.PropertyToID(SeasonShader.Shallow);
            topId = Shader.PropertyToID(SeasonShader.Top);
            foamId = Shader.PropertyToID(SeasonShader.Foam);
            rippleId = Shader.PropertyToID(SeasonShader.Ripple);
            ready = true;
            dirty = true;
        }

        public static void Invalidate() => dirty = true;

        public static void Tick()
        {
            if (!MeadworksHeadless.AllowsUi || Player.m_localPlayer == null)
            {
                if (applied) Clear();
                return;
            }
            if (!ready) return;
            var settings = Plugin.Settings;
            var view = SolsticeSeasons.View;
            if (settings == null || !settings.Get(SolsticeSettings.General.Enabled) || !view.Available || view.Season < 0 || view.Season >= Seasons.Count)
            {
                if (applied) Clear();
                return;
            }
            var season = (Season)view.Season;
            bool world = settings.Get(SolsticeSettings.Visuals.WorldTint);
            bool snow = settings.Get(SolsticeSettings.Visuals.SnowCover);
            bool water = settings.Get(SolsticeSettings.Visuals.FrozenWater);
            bool map = settings.Get(SolsticeSettings.Visuals.MinimapTint);
            float cover = snow ? ClimateRules.SnowCover(season, view.DayInSeason, view.DaysInSeason, false, false) : 0f;
            bool frozen = water && ClimateRules.WaterFrozen(season, view.DayInSeason);
            var stamp = new Stamp((int)season, snow ? Milli(cover) : 0, frozen, world, snow, water, map);
            if (!dirty && stamp.Equals(drawn))
            {
                if (map && !mapReady) ApplyMap(season, true);
                return;
            }
            dirty = false;
            if (!world && !snow && !water && !map)
            {
                if (applied) Clear();
                drawn = stamp;
                return;
            }
            try
            {
                Apply(season, cover, frozen, world, snow, water);
                ApplyMap(season, map);
                applied = true;
                drawn = stamp;
            }
            catch (Exception error)
            {
                drawn = stamp;
                if (warned) return;
                warned = true;
                log?.Warning("Season visuals: " + error.GetType().Name);
            }
        }

        public static void Shutdown()
        {
            try { if (applied || cache.Count > 0) Clear(); }
            catch (Exception error) { log?.Warning("Season visuals restore: " + error.GetType().Name); }
            ready = false;
            dirty = true;
            warned = false;
            drawn = default;
            loggedGrass = loggedTerrain = loggedSnow = loggedWater = loggedMap = false;
            log = null;
        }

        /// <summary>One diagnostics line. Empty work says off.</summary>
        public static string Describe()
        {
            if (!applied) return "visuals: off";
            var text = "visuals:";
            if (drawn.World) text += " world tint";
            if (drawn.Snow) text += " snow";
            if (drawn.Water) text += drawn.Frozen ? " ice tint" : " water";
            if (drawn.Map) text += " map";
            return text == "visuals:" ? "visuals: on" : text;
        }

        static void Apply(Season season, float cover, bool frozen, bool world, bool snow, bool water)
        {
            int grass = 0, leaf = 0, terrain = 0, snowCount = 0, waterCount = 0;
            seen.Clear();
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null) continue;
                string name = material.name;
                int id = material.GetInstanceID();
                if (!cache.TryGetValue(id, out var kept))
                {
                    var shader = material.shader;
                    if (shader == null) continue;
                    bool hasColor = material.HasProperty(colorId);
                    bool hasSnow = material.HasProperty(snowId);
                    bool hasShallow = material.HasProperty(shallowId);
                    bool hasTop = material.HasProperty(topId);
                    bool hasFoam = material.HasProperty(foamId);
                    bool hasRipple = material.HasProperty(rippleId);
                    var target = SeasonVisualSelection.Select(shader.name, name, hasColor, hasSnow, hasShallow, hasFoam, hasRipple, hasTop);
                    if (target == VisualTarget.None) continue;
                    kept = new Kept
                    {
                        Material = material,
                        Target = target,
                        Color = ((int)target & ColorMask) != 0 ? material.GetColor(colorId) : default,
                        Snow = (target & VisualTarget.Snow) != 0 ? material.GetFloat(snowId) : 0f,
                        Shallow = (target & VisualTarget.ShallowWater) != 0 ? material.GetColor(shallowId) : default,
                        Top = (target & VisualTarget.WaterTop) != 0 ? material.GetColor(topId) : default,
                        Foam = (target & VisualTarget.WaterFoam) != 0 ? material.GetColor(foamId) : default,
                        Ripple = (target & VisualTarget.WaterRipple) != 0 ? material.GetFloat(rippleId) : 0f
                    };
                    cache[id] = kept;
                }
                seen.Add(id);
                Paint(kept, season, cover, frozen, world, snow, water);
                if ((kept.Target & VisualTarget.Grass) != 0) grass++;
                if ((kept.Target & VisualTarget.Leaf) != 0) leaf++;
                if ((kept.Target & VisualTarget.Terrain) != 0) terrain++;
                if ((kept.Target & VisualTarget.Snow) != 0) snowCount++;
                if (((int)kept.Target & WaterMask) != 0) waterCount++;
            }
            drop.Clear();
            foreach (var pair in cache)
            {
                if (seen.Contains(pair.Key) && pair.Value.Material != null) continue;
                if (pair.Value.Material != null) Restore(pair.Value);
                drop.Add(pair.Key);
            }
            for (int i = 0; i < drop.Count; i++) cache.Remove(drop[i]);
            Note(world, grass, leaf, terrain, snow, snowCount, water, waterCount);
        }

        static void Note(bool world, int grass, int leaf, int terrain, bool snow, int snowCount, bool water, int waterCount)
        {
            if (ZNetScene.instance == null) return;
            if (world && grass == 0 && leaf == 0 && !loggedGrass)
            {
                loggedGrass = true;
                log?.Info("World tint found no grass or leaf material with _Color. Those colors stay the game's.");
            }
            if (world && terrain == 0 && !loggedTerrain)
            {
                loggedTerrain = true;
                log?.Info("World tint found no terrain material with _Color. Ground color stays the game's.");
            }
            if (snow && snowCount == 0 && !loggedSnow)
            {
                loggedSnow = true;
                log?.Info("Snow cover found no material with _SnowLevel. Ground snow is the terrain mesh. Solstice does not save snow onto buildings.");
            }
            if (water && waterCount == 0 && !loggedWater)
            {
                loggedWater = true;
                log?.Info("Frozen water found no shallow color, top color, foam or ripple distance. The water stays the game's. Solstice does not add ice you can walk on.");
            }
        }

        static void Paint(in Kept kept, Season season, float cover, bool frozen, bool world, bool snow, bool water)
        {
            var material = kept.Material;
            if (material == null) return;
            var target = kept.Target;
            if (((int)target & WorldColor) != 0)
            {
                var channel = (target & VisualTarget.Leaf) != 0 ? TintChannel.Leaf
                    : (target & VisualTarget.Terrain) != 0 ? TintChannel.Terrain : TintChannel.Grass;
                // A white multiplier only reveals the texture. Snow Cover paints the mesh and a texture copy instead.
                var color = world && !(snow && cover > 0f) ? Tint(kept.Color, channel, season, false) : kept.Color;
                material.SetColor(colorId, color);
            }
            if ((target & VisualTarget.Snow) != 0)
                material.SetFloat(snowId, snow ? SeasonTint.SnowApply(kept.Snow, cover) : kept.Snow);
            if ((target & VisualTarget.ShallowWater) != 0)
                material.SetColor(shallowId, water ? Tint(kept.Shallow, TintChannel.ShallowWater, season, frozen) : kept.Shallow);
            if ((target & VisualTarget.WaterTop) != 0)
                material.SetColor(topId, water ? Ice(kept.Top, frozen, SeasonTint.SurfaceIceBlend) : kept.Top);
            if ((target & VisualTarget.WaterFoam) != 0)
                material.SetColor(foamId, water ? Ice(kept.Foam, frozen, SeasonTint.FoamIceBlend) : kept.Foam);
            if ((target & VisualTarget.WaterRipple) != 0)
                material.SetFloat(rippleId, water ? SeasonTint.CalmRipple(kept.Ripple, frozen) : kept.Ripple);
        }

        static void Restore(in Kept kept) => Paint(kept, Season.Spring, 0f, false, false, false, false);

        static void ApplyMap(Season season, bool on)
        {
            var minimap = Minimap.instance;
            if (minimap == null)
            {
                mapReady = false;
                return;
            }
            TintImage(minimap.m_mapImageSmall, ref smallImage, ref smallOriginal, ref smallStored, on, season);
            TintImage(minimap.m_mapImageLarge, ref largeImage, ref largeOriginal, ref largeStored, on, season);
            mapReady = true;
            if (on && minimap.m_mapImageSmall == null && minimap.m_mapImageLarge == null && !loggedMap && ZNetScene.instance != null)
            {
                loggedMap = true;
                log?.Info("Minimap tint found no map image. The maps stay the game's.");
            }
        }

        static void TintImage(RawImage image, ref RawImage keptImage, ref Color original, ref bool stored, bool on, Season season)
        {
            if (image == null) return;
            if (stored && keptImage != image)
            {
                if (keptImage != null) keptImage.color = original;
                stored = false;
            }
            if (!stored)
            {
                original = image.color;
                keptImage = image;
                stored = true;
            }
            image.color = on ? Tint(original, TintChannel.Map, season, false) : original;
        }

        static void RestoreMap()
        {
            // The singleton can disappear before its images, so restore the objects whose colors we captured.
            if (smallStored && smallImage != null) smallImage.color = smallOriginal;
            if (largeStored && largeImage != null) largeImage.color = largeOriginal;
            smallStored = false;
            largeStored = false;
            smallImage = null;
            largeImage = null;
            mapReady = false;
        }

        static void Clear()
        {
            foreach (var pair in cache)
                if (pair.Value.Material != null) Restore(pair.Value);
            cache.Clear();
            seen.Clear();
            RestoreMap();
            applied = false;
        }

        static Color Tint(Color original, TintChannel channel, Season season, bool frozen)
        {
            var next = SeasonTint.Apply(channel, season, new Rgb(original.r, original.g, original.b), frozen);
            return new Color(next.R, next.G, next.B, original.a);
        }

        static Color Ice(Color original, bool frozen, float blend)
        {
            var next = SeasonTint.FreezeAmount(new Rgb(original.r, original.g, original.b), frozen, blend);
            return new Color(next.R, next.G, next.B, original.a);
        }

        static int Milli(float cover)
        {
            if (float.IsNaN(cover) || cover <= 0f) return 0;
            int milli = (int)Math.Round(cover * 1000.0);
            return milli < 0 ? 0 : milli;
        }
    }

    /// <summary>Picks up materials that load with the world, once, instead of scanning every frame.</summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    static class SeasonVisualScene
    {
        static void Postfix() => SeasonVisuals.Invalidate();
    }
}
