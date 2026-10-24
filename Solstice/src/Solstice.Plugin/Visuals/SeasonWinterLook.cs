// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Client winter ground. The saved terrain paint mask is never written.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Config;
using ExtraPotions.Solstice.Effects;
using ExtraPotions.Solstice.Visuals;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Snow on this client. Each source texture is read once per winter session. A later day reuses those pixels
    /// and swaps a cached strength step in when it is ready. Terrain chunks rebuild a few per frame, nearest first.
    /// A headless server does none of this. Disable and unload put the captured textures, clutter and mesh colors back.
    /// </summary>
    public static class SeasonWinterLook
    {
        sealed class Kept
        {
            public Material Material;
            public Texture Original;
            public int SourceId;
            public bool Leaves;
            public int ShownStep;
        }

        sealed class SourceImage
        {
            public string Name;
            public Color32[] Pixels;
            public int Width;
            public int Height;
            public TextureWrapMode Wrap;
            public FilterMode Filter;
        }

        readonly struct FrostKey : IEquatable<FrostKey>
        {
            public FrostKey(int source, int step)
            {
                Source = source;
                Step = step;
            }

            public int Source { get; }
            public int Step { get; }

            public bool Equals(FrostKey other) => Source == other.Source && Step == other.Step;
            public override bool Equals(object obj) => obj is FrostKey other && Equals(other);
            public override int GetHashCode() => (Source * 397) ^ Step;
        }

        sealed class BakeJob
        {
            public FrostKey Key;
            public bool Leaves;
            public Texture2D Texture;
            public Color32[] Pixels;
            public int Generation;
            public int Phase;
            public string Error;
        }

        const int FrostEdge = 512;

        static readonly Dictionary<int, Kept> saved = new Dictionary<int, Kept>();
        static readonly Dictionary<int, SourceImage> originals = new Dictionary<int, SourceImage>();
        static readonly Dictionary<FrostKey, Texture2D> cache = new Dictionary<FrostKey, Texture2D>();
        static readonly Queue<BakeJob> pending = new Queue<BakeJob>();
        static readonly HashSet<FrostKey> waiting = new HashSet<FrostKey>();
        static readonly Dictionary<int, ClutterBase> bases = new Dictionary<int, ClutterBase>();
        static readonly List<Heightmap> maps = new List<Heightmap>();
        static readonly List<FrostKey> dropKeys = new List<FrostKey>();
        static readonly object gate = new object();

        struct ClutterBase
        {
            public bool Enabled;
            public int Amount;
            public float Min;
            public float Max;
        }

        static MeadworksLog log;
        static MethodInfo rebuild;
        static BakeJob active;
        static int mainTexId;
        static int depth;
        static int generation;
        static int desiredStep = -1;
        static int targetMilli = int.MinValue;
        static int clutterMilli = int.MinValue;
        static int terrainMilli = int.MinValue;
        static int terrainIndex;
        static int reads, uploads, terrainDone, frames, peakMs;
        static long passStart, beginTicks, clutterTicks, textureTicks, terrainTicks, managedTicks, readTicks, uploadTicks, readPeakTicks, uploadPeakTicks;
        static int passFrame, passGc0, passGc1, passGc2, readPeakFrame, uploadPeakFrame, managedPeakFrame;
        static bool dirty = true, painted, warned, logging, warnedSpike, debugLog;
        static double nextSlowWarn;
        static readonly Stopwatch slowClock = Stopwatch.StartNew();
        static bool loggedRebuild, loggedRead, loggedNone;

        /// <summary>Cover used while a terrain mesh is rebuilding. 0 leaves the game's biome mask.</summary>
        public static float Cover { get; private set; }

        public static void Initialize(MeadworksLog logger)
        {
            log = logger;
            mainTexId = Shader.PropertyToID("_MainTex");
            dirty = true;
        }

        public static void Invalidate()
        {
            dirty = true;
            clutterMilli = int.MinValue;
            ResetTerrain();
        }

        public static void Tick()
        {
            if (!MeadworksHeadless.AllowsUi || Player.m_localPlayer == null)
            {
                ClearActive(true);
                return;
            }
            try
            {
                float cover = ReadCover();
                int coverMilli = Milli(cover);
                int step = SeasonGround.FrostStep(cover);
                if (!dirty && step == desiredStep && coverMilli == targetMilli
                    && clutterMilli == coverMilli && terrainMilli == coverMilli && TexturesIdle())
                    return;
                var frame = Stopwatch.StartNew();
                bool changed = dirty || step != desiredStep || coverMilli != targetMilli;
                if (changed)
                {
                    BeginPass(cover, step, coverMilli);
                    if (debugLog) beginTicks += frame.ElapsedTicks;
                }
                long phaseStart = debugLog ? Stopwatch.GetTimestamp() : 0;
                Cover = cover;
                bool cluttered = false;
                if (clutterMilli != coverMilli)
                {
                    if (!(coverMilli <= 0 && bases.Count == 0)) ApplyClutter(cover);
                    clutterMilli = coverMilli;
                    cluttered = true;
                }
                if (debugLog) clutterTicks += Stopwatch.GetTimestamp() - phaseStart;
                if (!cluttered)
                {
                    if (debugLog) phaseStart = Stopwatch.GetTimestamp();
                    if (!TexturesIdle()) PumpTextures();
                    if (debugLog)
                    {
                        textureTicks += Stopwatch.GetTimestamp() - phaseStart;
                        phaseStart = Stopwatch.GetTimestamp();
                    }
                    if (terrainMilli != coverMilli && frame.Elapsed.TotalMilliseconds < 30) PumpTerrain();
                    if (debugLog) terrainTicks += Stopwatch.GetTimestamp() - phaseStart;
                }
                if (debugLog) managedTicks += frame.ElapsedTicks;
                Note(frame.Elapsed.TotalMilliseconds);
                if (TexturesIdle() && clutterMilli == coverMilli && terrainMilli == coverMilli) FinishPass(true);
            }
            catch (Exception error)
            {
                dirty = false;
                clutterMilli = targetMilli;
                terrainMilli = targetMilli;
                pending.Clear();
                waiting.Clear();
                if (warned) return;
                warned = true;
                log?.Warning("Season winter look: " + error.GetType().Name);
            }
        }

        public static void Shutdown()
        {
            try { ClearActive(true); }
            catch (Exception error) { log?.Warning("Season winter look restore: " + error.GetType().Name); }
            saved.Clear();
            bases.Clear();
            dirty = true;
            warned = false;
            loggedRebuild = false;
            loggedRead = false;
            loggedNone = false;
            log = null;
        }

        static float ReadCover()
        {
            var settings = Plugin.Settings;
            var view = SolsticeSeasons.View;
            if (settings == null || !settings.Get(SolsticeSettings.General.Enabled) || !view.Available
                || view.Season < 0 || view.Season >= Seasons.Count || !settings.Get(SolsticeSettings.Visuals.SnowCover))
                return 0f;
            return ClimateRules.SnowCover((Season)view.Season, view.DayInSeason, view.DaysInSeason, false, false);
        }

        static void BeginPass(float cover, int step, int coverMilli)
        {
            if (logging) FinishPass();
            reads = uploads = terrainDone = frames = peakMs = 0;
            warnedSpike = false;
            debugLog = log != null && log.IsEnabled(LogSeverity.Debug);
            logging = true;
            if (debugLog)
            {
                passStart = Stopwatch.GetTimestamp(); passFrame = UnityEngine.Time.frameCount;
                readPeakFrame = uploadPeakFrame = managedPeakFrame = passFrame;
                passGc0 = GC.CollectionCount(0); passGc1 = GC.CollectionCount(1); passGc2 = GC.CollectionCount(2);
                beginTicks = clutterTicks = textureTicks = terrainTicks = managedTicks = readTicks = uploadTicks = readPeakTicks = uploadPeakTicks = 0;
            }
            dirty = false;
            targetMilli = coverMilli;
            desiredStep = step;
            Cover = cover;
            if (step <= 0)
            {
                RestoreTextures();
                DropFrost();
                saved.Clear();
                if (!painted) terrainMilli = coverMilli;
                else ResetTerrain();
                return;
            }
            painted = true;
            Scan();
            if (saved.Count == 0 && !loggedNone)
            {
                loggedNone = true;
                log?.Info("Snow cover found no grass or leaf texture. Ground snow is still the terrain mesh.");
            }
            pending.Clear();
            waiting.Clear();
            AssignReady();
            EnqueueMissing();
            ResetTerrain();
        }

        static bool TexturesIdle() => desiredStep <= 0 || (pending.Count == 0 && active == null);

        static void ClearActive(bool immediate)
        {
            if (!painted && saved.Count == 0 && bases.Count == 0 && cache.Count == 0 && active == null && originals.Count == 0)
                return;
            Cover = 0f;
            desiredStep = 0;
            targetMilli = 0;
            RestoreTextures();
            DropFrost();
            saved.Clear();
            ApplyClutter(0f);
            bases.Clear();
            clutterMilli = 0;
            if (immediate)
            {
                if (CopyMaps())
                {
                    var clock = Stopwatch.StartNew();
                    int count = maps.Count;
                    RebuildAll();
                    log?.Info("Snow cover restore: " + count + " chunks in " + (int)clock.Elapsed.TotalMilliseconds + " ms.");
                }
                maps.Clear();
                terrainIndex = 0;
                terrainMilli = 0;
                painted = false;
            }
            else ResetTerrain();
            dirty = false;
            logging = false;
        }

        static void Scan()
        {
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            var seen = new HashSet<int>();
            if (materials == null)
            {
                saved.Clear();
                return;
            }
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null || material.shader == null) continue;
                if (!material.HasProperty(mainTexId)) continue;
                var shown = material.GetTexture(mainTexId);
                bool showingFrost = shown != null && IsFrost(shown);
                string textureName = shown != null && !showingFrost ? shown.name : null;
                if (!SeasonVisualSelection.FrostTexture(material.shader.name, material.name, textureName, out bool leaves)) continue;
                int id = material.GetInstanceID();
                seen.Add(id);
                if (saved.TryGetValue(id, out var kept) && kept.Original != null)
                {
                    kept.Material = material;
                    if (!showingFrost || leaves) kept.Leaves = leaves;
                    continue;
                }
                if (shown == null || showingFrost) continue;
                saved[id] = new Kept
                {
                    Material = material,
                    Original = shown,
                    SourceId = shown.GetInstanceID(),
                    Leaves = leaves
                };
            }
            if (seen.Count == saved.Count) return;
            var drop = new List<int>();
            foreach (var id in saved.Keys)
                if (!seen.Contains(id)) drop.Add(id);
            for (int i = 0; i < drop.Count; i++) saved.Remove(drop[i]);
        }

        static void EnqueueMissing()
        {
            foreach (var kept in saved.Values)
            {
                if (kept.Original == null) continue;
                var key = new FrostKey(kept.SourceId, desiredStep);
                if (cache.ContainsKey(key) || waiting.Contains(key) || (active != null && active.Key.Equals(key))) continue;
                waiting.Add(key);
                pending.Enqueue(new BakeJob { Key = key, Leaves = LeavesFor(kept.SourceId) });
            }
        }

        static bool LeavesFor(int sourceId)
        {
            foreach (var kept in saved.Values)
                if (kept.SourceId == sourceId && kept.Leaves) return true;
            return false;
        }

        static void AssignReady()
        {
            if (desiredStep <= 0) return;
            foreach (var kept in saved.Values)
            {
                if (kept.Material == null) continue;
                if (!cache.TryGetValue(new FrostKey(kept.SourceId, desiredStep), out var tex) || tex == null) continue;
                if (kept.ShownStep == desiredStep && kept.Material.GetTexture(mainTexId) == tex) continue;
                kept.Material.SetTexture(mainTexId, tex);
                kept.ShownStep = desiredStep;
            }
        }

        static void PumpTextures()
        {
            if (active != null)
            {
                int phase, gen;
                Color32[] pixels;
                string error;
                lock (gate)
                {
                    phase = active.Phase;
                    gen = active.Generation;
                    pixels = active.Pixels;
                    error = active.Error;
                }
                if (gen != generation || phase == 3)
                {
                    if (phase == 3 && !loggedRead)
                    {
                        loggedRead = true;
                        log?.Warning("Season frost texture: " + (string.IsNullOrEmpty(error) ? "failed" : error));
                    }
                    Discard(active);
                    active = null;
                    return;
                }
                if (phase != 2) return;
                Upload(active, pixels);
                active = null;
                AssignReady();
                return;
            }
            if (pending.Count == 0) return;
            var job = pending.Dequeue();
            waiting.Remove(job.Key);
            active = job;
            if (!originals.ContainsKey(job.Key.Source) && !ReadSource(job))
            {
                active = null;
                return;
            }
            StartWorker(job);
        }

        // Player builds cannot compile a frost shader. Blit only makes a readable copy.
        static bool ReadSource(BakeJob job)
        {
            long start = debugLog ? Stopwatch.GetTimestamp() : 0;
            try { return ReadSourceCore(job); }
            finally
            {
                if (debugLog)
                {
                    long ticks = Stopwatch.GetTimestamp() - start;
                    readTicks += ticks;
                    if (ticks > readPeakTicks) { readPeakTicks = ticks; readPeakFrame = UnityEngine.Time.frameCount; }
                }
            }
        }
        static bool ReadSourceCore(BakeJob job)
        {
            Texture source = null;
            foreach (var kept in saved.Values)
                if (kept.SourceId == job.Key.Source && kept.Original != null) { source = kept.Original; break; }
            if (source == null) return false;
            int width = source.width;
            int height = source.height;
            if (width < 2 || height < 2) return false;
            float scale = 1f;
            int edge = width > height ? width : height;
            if (edge > FrostEdge) scale = FrostEdge / (float)edge;
            int bakeWidth = Math.Max(1, (int)Math.Round(width * scale));
            int bakeHeight = Math.Max(1, (int)Math.Round(height * scale));
            // sRGB target. A linear one stores decoded albedo bytes, and the sRGB texture decodes them again.
            var target = RenderTexture.GetTemporary(bakeWidth, bakeHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var filter = source.filterMode;
            Texture2D readable = null;
            try
            {
                // Point sample. A filtered blit averages cutout alpha into blocks; the shader still filters when it draws.
                source.filterMode = FilterMode.Point;
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                readable = new Texture2D(bakeWidth, bakeHeight, TextureFormat.RGBA32, true, false);
                readable.name = "SolsticeFrost";
                readable.wrapMode = source.wrapMode;
                readable.filterMode = filter;
                readable.hideFlags = HideFlags.HideAndDontSave;
                readable.ReadPixels(new Rect(0f, 0f, bakeWidth, bakeHeight), 0, 0);
                readable.Apply(false, false);
                originals[job.Key.Source] = new SourceImage
                {
                    Name = source.name,
                    Pixels = readable.GetPixels32(),
                    Width = bakeWidth,
                    Height = bakeHeight,
                    Wrap = source.wrapMode,
                    Filter = filter
                };
                job.Texture = readable;
                readable = null;
                reads++;
                return true;
            }
            catch (Exception error)
            {
                if (readable != null) UnityEngine.Object.Destroy(readable);
                if (!loggedRead)
                {
                    loggedRead = true;
                    log?.Warning("Season frost texture: " + error.GetType().Name);
                }
                return false;
            }
            finally
            {
                source.filterMode = filter;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        static void StartWorker(BakeJob job)
        {
            if (!originals.TryGetValue(job.Key.Source, out var image) || image.Pixels == null)
            {
                active = null;
                return;
            }
            var source = (Color32[])image.Pixels.Clone();
            float amount = SeasonGround.FrostAmount(job.Key.Step);
            bool leaves = job.Leaves;
            int gen;
            lock (gate)
            {
                gen = generation;
                job.Generation = gen;
                job.Phase = 1;
            }
            try
            {
                Task.Run(() => FinishJob(job, source, amount, leaves, gen));
            }
            catch (Exception)
            {
                FinishJob(job, source, amount, leaves, gen);
            }
        }

        static void FinishJob(BakeJob job, Color32[] source, float amount, bool leaves, int gen)
        {
            try
            {
                var pixels = FrostPixels(source, amount, leaves);
                lock (gate)
                {
                    if (gen != generation) return;
                    job.Pixels = pixels;
                    job.Phase = 2;
                }
            }
            catch (Exception error)
            {
                lock (gate)
                {
                    if (gen != generation) return;
                    job.Error = error.GetType().Name;
                    job.Phase = 3;
                }
            }
        }

        static Color32[] FrostPixels(Color32[] source, float amount, bool leaves)
        {
            var bytes = new byte[source.Length * 4];
            for (int i = 0; i < source.Length; i++)
            {
                bytes[i * 4] = source[i].r;
                bytes[i * 4 + 1] = source[i].g;
                bytes[i * 4 + 2] = source[i].b;
                bytes[i * 4 + 3] = source[i].a;
            }
            SeasonGround.Frost(bytes, amount, leaves);
            var pixels = new Color32[source.Length];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(bytes[i * 4], bytes[i * 4 + 1], bytes[i * 4 + 2], bytes[i * 4 + 3]);
            return pixels;
        }

        static void Upload(BakeJob job, Color32[] pixels)
        {
            long start = debugLog ? Stopwatch.GetTimestamp() : 0;
            try { UploadCore(job, pixels); }
            finally
            {
                if (debugLog)
                {
                    long ticks = Stopwatch.GetTimestamp() - start;
                    uploadTicks += ticks;
                    if (ticks > uploadPeakTicks) { uploadPeakTicks = ticks; uploadPeakFrame = UnityEngine.Time.frameCount; }
                }
            }
        }
        static void UploadCore(BakeJob job, Color32[] pixels)
        {
            if (pixels == null || !originals.TryGetValue(job.Key.Source, out var image)) return;
            var texture = job.Texture;
            if (texture == null)
            {
                texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, true, false);
                texture.name = "SolsticeFrost";
                texture.wrapMode = image.Wrap;
                texture.filterMode = image.Filter;
                texture.hideFlags = HideFlags.HideAndDontSave;
            }
            NoteSample(job.Key.Source, image.Name, image.Pixels, pixels);
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            job.Texture = null;
            Remember(job.Key, texture);
            uploads++;
        }

        static void Remember(FrostKey key, Texture2D texture)
        {
            if (cache.TryGetValue(key, out var previous) && previous != null && previous != texture)
                UnityEngine.Object.Destroy(previous);
            cache[key] = texture;
            dropKeys.Clear();
            foreach (var pair in cache)
            {
                if (pair.Key.Source != key.Source || pair.Key.Equals(key) || InUse(pair.Key)) continue;
                dropKeys.Add(pair.Key);
            }
            for (int i = 0; i < dropKeys.Count; i++)
            {
                if (!cache.TryGetValue(dropKeys[i], out var old)) continue;
                cache.Remove(dropKeys[i]);
                if (old != null) UnityEngine.Object.Destroy(old);
            }
        }

        static bool InUse(FrostKey key)
        {
            if (key.Step == desiredStep) return true;
            foreach (var kept in saved.Values)
                if (kept.SourceId == key.Source && kept.ShownStep == key.Step) return true;
            return false;
        }

        static void Discard(BakeJob job)
        {
            if (job.Texture != null)
                UnityEngine.Object.Destroy(job.Texture);
            job.Texture = null;
        }

        static void RestoreTextures()
        {
            foreach (var kept in saved.Values)
                if (kept.Material != null && kept.Original != null)
                    kept.Material.SetTexture(mainTexId, kept.Original);
        }

        static void DropFrost()
        {
            lock (gate) generation++;
            if (active != null)
            {
                Discard(active);
                active = null;
            }
            pending.Clear();
            waiting.Clear();
            foreach (var pair in cache)
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            cache.Clear();
            originals.Clear();
        }

        static bool IsFrost(Texture texture) =>
            texture != null && texture.name != null && texture.name.StartsWith("SolsticeFrost", StringComparison.Ordinal);

        static void ApplyClutter(float cover)
        {
            var system = ClutterSystem.instance;
            if (system == null || system.m_clutter == null) return;
            bool changed = false;
            var list = system.m_clutter;
            for (int i = 0; i < list.Count; i++)
            {
                var clutter = list[i];
                if (clutter == null) continue;
                if (!bases.TryGetValue(i, out var basis))
                {
                    basis = new ClutterBase
                    {
                        Enabled = clutter.m_enabled,
                        Amount = clutter.m_amount,
                        Min = clutter.m_scaleMin,
                        Max = clutter.m_scaleMax
                    };
                    bases[i] = basis;
                }
                string name = clutter.m_name ?? "";
                if (clutter.m_prefab != null && clutter.m_prefab.name != null)
                    name = name.Length == 0 ? clutter.m_prefab.name : name + " " + clutter.m_prefab.name;
                int bits = (int)clutter.m_biome;
                bool snowy = bits != 0 && (bits & ~SeasonGround.SnowBiomes) == 0;
                bool enabled = basis.Enabled;
                int amount = basis.Amount;
                float min = basis.Min;
                float max = basis.Max;
                if (cover > 0f && snowy && SeasonGround.HideBloom(name, cover)) enabled = false;
                else if (cover > 0f && snowy && SeasonGround.IsGrass(name))
                {
                    amount = SeasonGround.GrassCount(basis.Amount, cover);
                    min = SeasonGround.GrassScale(basis.Min, cover);
                    max = SeasonGround.GrassScale(basis.Max, cover);
                }
                if (clutter.m_enabled != enabled) { clutter.m_enabled = enabled; changed = true; }
                if (clutter.m_amount != amount) { clutter.m_amount = amount; changed = true; }
                if (clutter.m_scaleMin != min) { clutter.m_scaleMin = min; changed = true; }
                if (clutter.m_scaleMax != max) { clutter.m_scaleMax = max; changed = true; }
            }
            if (changed) system.ClearAll();
            if (cover <= 0f) bases.Clear();
        }

        static bool CopyMaps()
        {
            maps.Clear();
            var live = Heightmap.GetAllHeightmaps();
            if (live == null) return false;
            for (int i = 0; i < live.Count; i++)
                if (live[i] != null) maps.Add(live[i]);
            return maps.Count > 0;
        }

        static void ResetTerrain()
        {
            maps.Clear();
            terrainIndex = 0;
            terrainMilli = int.MinValue;
        }

        static void SortMaps()
        {
            Vector3 place = default;
            bool have = false;
            var player = Player.m_localPlayer;
            if (player != null && player.transform != null)
            {
                place = player.transform.position;
                have = true;
            }
            maps.Sort((a, b) =>
            {
                if (!have) return 0;
                return Planar(a, place).CompareTo(Planar(b, place));
            });
        }

        static float Planar(Heightmap map, Vector3 place)
        {
            if (map == null || map.transform == null) return float.MaxValue;
            var point = map.transform.position;
            float x = point.x - place.x;
            float z = point.z - place.z;
            return x * x + z * z;
        }

        static void PumpTerrain()
        {
            if (terrainMilli == targetMilli) return;
            if (maps.Count == 0)
            {
                if (!CopyMaps())
                {
                    terrainMilli = targetMilli;
                    return;
                }
                SortMaps();
                terrainIndex = 0;
            }
            int done = 0;
            var clock = Stopwatch.StartNew();
            while (terrainIndex < maps.Count
                && SeasonGround.NextChunk(done, clock.Elapsed.TotalMilliseconds, maps.Count - terrainIndex) == 1)
            {
                var map = maps[terrainIndex];
                terrainIndex++;
                Rebuild(map);
                done++;
                terrainDone++;
            }
            if (terrainIndex >= maps.Count) terrainMilli = targetMilli;
        }

        static void RebuildAll()
        {
            for (int i = 0; i < maps.Count; i++) Rebuild(maps[i]);
        }

        static void Rebuild(Heightmap map)
        {
            if (map == null || !MeadworksHeadless.AllowsUi) return;
            if (rebuild == null) rebuild = AccessTools.Method(typeof(Heightmap), "RebuildRenderMesh");
            if (rebuild == null)
            {
                if (loggedRebuild) return;
                loggedRebuild = true;
                log?.Warning("Season winter look: terrain rebuild is missing.");
                return;
            }
            try { rebuild.Invoke(map, null); }
            catch (Exception error)
            {
                if (loggedRebuild) return;
                loggedRebuild = true;
                log?.Warning("Season terrain rebuild: " + error.GetType().Name);
            }
        }

        static void NoteSample(int sourceId, string textureName, Color32[] source, Color32[] frost)
        {
            if (!WorthSample(textureName)) return;
            if (!MeanOpaque(source, out int sr, out int sg, out int sb, out int count)) return;
            if (!MeanOpaque(frost, out int fr, out int fg, out int fb, out _)) return;
            string label = SampleLabel(sourceId, textureName);
            if (debugLog)
                log?.Debug("Snow cover " + label + ": " + count + " texels, source " + sr + " " + sg + " " + sb
                    + ", frost " + fr + " " + fg + " " + fb);
            if (sg >= 48 && fg < 40 && fg * 2 < sg)
                log?.Warning("Snow cover " + label + " went dark. Source green " + sg + ", frost green " + fg + ".");
        }

        static bool WorthSample(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (Has(name, "log") || Has(name, "bark") || Has(name, "trunk")) return false;
            return Has(name, "pine_tree") || Has(name, "forest_groundcover") || Has(name, "ormbunke")
                || Has(name, "clutter_shrub") || Has(name, "beech_leaf") || Has(name, "shrub_2");
        }

        static bool Has(string text, string part) => text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        static string SampleLabel(int sourceId, string textureName)
        {
            foreach (var kept in saved.Values)
            {
                if (kept.SourceId != sourceId || kept.Material == null || string.IsNullOrEmpty(kept.Material.name)) continue;
                return kept.Material.name + " / " + textureName;
            }
            return textureName ?? "";
        }

        static bool MeanOpaque(Color32[] pixels, out int r, out int g, out int b, out int count)
        {
            r = g = b = count = 0;
            if (pixels == null || pixels.Length == 0) return false;
            long sr = 0, sg = 0, sb = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0) continue;
                sr += pixels[i].r;
                sg += pixels[i].g;
                sb += pixels[i].b;
                count++;
            }
            if (count == 0) return false;
            r = (int)Math.Round(sr / (double)count);
            g = (int)Math.Round(sg / (double)count);
            b = (int)Math.Round(sb / (double)count);
            return true;
        }

        static string LightText()
        {
            var env = EnvMan.instance;
            var sky = env != null ? env.m_currentEnv : null;
            if (sky == null) return "light unavailable";
            string text = sky.m_name
                + " sun " + Rgb(sky.m_sunColorDay)
                + " ambient " + Rgb(sky.m_ambColorDay)
                + " intensity " + sky.m_lightIntensityDay.ToString("0.00", CultureInfo.InvariantCulture);
            var lamp = env.m_dirLight;
            if (lamp != null)
                text += " dir " + Rgb(lamp.color) + " x" + lamp.intensity.ToString("0.00", CultureInfo.InvariantCulture);
            var live = RenderSettings.ambientLight;
            text += " live ambient " + Rgb(live);
            return text;
        }

        static string Rgb(Color color) =>
            color.r.ToString("0.00", CultureInfo.InvariantCulture) + " "
            + color.g.ToString("0.00", CultureInfo.InvariantCulture) + " "
            + color.b.ToString("0.00", CultureInfo.InvariantCulture);

        static void Note(double milliseconds)
        {
            frames++;
            int whole = milliseconds <= 0d ? 0 : (int)Math.Ceiling(milliseconds);
            if (whole > peakMs)
            {
                peakMs = whole;
                if (debugLog) managedPeakFrame = UnityEngine.Time.frameCount;
            }
            if (milliseconds > 50d && !warnedSpike && WarnSlow("Snow cover frame " + whole + " ms."))
                warnedSpike = true;
        }

        static bool WarnSlow(string message)
        {
            if (!TickGuard.Report(ref nextSlowWarn, slowClock.Elapsed.TotalSeconds)) return false;
            log?.Warning(message);
            return true;
        }

        static void FinishPass(bool complete = false)
        {
            if (!logging) return;
            if (desiredStep <= 0 && terrainMilli == targetMilli) painted = false;
            logging = false;
            if (!debugLog && desiredStep <= 0 && reads == 0 && uploads == 0 && terrainDone == 0) return;
            if (debugLog && log != null && log.IsEnabled(LogSeverity.Debug))
            {
                long passEnd = Stopwatch.GetTimestamp();
                int gc0 = GC.CollectionCount(0) - passGc0, gc1 = GC.CollectionCount(1) - passGc1, gc2 = GC.CollectionCount(2) - passGc2;
                try
                {
                    log.Debug("Snow cover pass: step " + desiredStep
                    + ", reads " + reads
                    + ", uploads " + uploads
                    + ", sources " + originals.Count
                    + ", terrain " + terrainDone
                    + ", frames " + frames
                    + ", peak " + peakMs + " ms, "
                    + LightText() + ". StallTiming Solstice.winter complete=" + complete
                    + " frame=" + passFrame + " endFrame=" + UnityEngine.Time.frameCount
                    + " thread=" + System.Threading.Thread.CurrentThread.ManagedThreadId
                    + " startTicks=" + passStart + " endTicks=" + passEnd
                    + " elapsedMs=" + Milliseconds(passEnd - passStart)
                    + " managedMs=" + Milliseconds(managedTicks) + " beginMs=" + Milliseconds(beginTicks)
                    + " clutterMs=" + Milliseconds(clutterTicks) + " texturesMs=" + Milliseconds(textureTicks)
                    + " terrainMs=" + Milliseconds(terrainTicks) + " readMs=" + Milliseconds(readTicks)
                    + " uploadMs=" + Milliseconds(uploadTicks) + " readPeakMs=" + Milliseconds(readPeakTicks)
                    + " uploadPeakMs=" + Milliseconds(uploadPeakTicks)
                    + " readPeakFrame=" + readPeakFrame + " uploadPeakFrame=" + uploadPeakFrame + " managedPeakFrame=" + managedPeakFrame
                    + " gc=" + gc0 + "/" + gc1 + "/" + gc2);
                }
                catch { } // Diagnostic output must not abort winter work.
            }
            if (peakMs > 50 && !warnedSpike)
                WarnSlow("Snow cover peak frame " + peakMs + " ms.");
        }

        static string Milliseconds(long ticks) => (ticks * 1000d / Stopwatch.Frequency).ToString("0.000", CultureInfo.InvariantCulture);

        static int Milli(float cover)
        {
            if (float.IsNaN(cover) || cover <= 0f) return 0;
            if (cover > 1f) cover = 1f;
            return (int)Math.Round(cover * 1000.0);
        }

        static void ResetScene()
        {
            try
            {
                RestoreTextures();
                DropFrost();
            }
            catch (Exception error)
            {
                log?.Warning("Season winter look scene: " + error.GetType().Name);
            }
            saved.Clear();
            bases.Clear();
            painted = false;
            dirty = true;
            desiredStep = -1;
            targetMilli = int.MinValue;
            clutterMilli = int.MinValue;
            ResetTerrain();
            logging = false;
        }

        [HarmonyPatch(typeof(Heightmap), "RebuildRenderMesh")]
        static class SeasonTerrainRebuild
        {
            static void Prefix(ref bool __state)
            {
                __state = MeadworksHeadless.AllowsUi;
                if (__state) depth++;
            }

            static void Finalizer(bool __state)
            {
                if (__state && depth > 0) depth--;
            }
        }

        [HarmonyPatch(typeof(Heightmap), nameof(Heightmap.GetBiomeColor), new[] { typeof(Heightmap.Biome) })]
        static class SeasonTerrainColor
        {
            static void Postfix(Heightmap.Biome biome, ref Color32 __result)
            {
                if (depth <= 0 || Cover <= 0f || !MeadworksHeadless.AllowsUi) return;
                byte r = __result.r, g = __result.g, b = __result.b, a = __result.a;
                SeasonGround.Paint((int)biome, ref r, ref g, ref b, ref a, Cover);
                __result = new Color32(r, g, b, a);
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        static class SeasonWinterScene
        {
            static void Postfix() => ResetScene();
        }
    }
}
