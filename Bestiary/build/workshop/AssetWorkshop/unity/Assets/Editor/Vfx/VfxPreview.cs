using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>
    /// Renders an effect over time without Play mode: our prefab at the origin and each game reference beside it (staged
    /// under Assets/Reference, preview only), every material swapped for its preview copy (<see cref="VfxEmulation"/>),
    /// the particle systems stepped frame by frame with Simulate, each frame rendered as linear HDR and written raw
    /// (RGBA half floats, bottom row first) as frame_NNNN.half. vfx/build.py grades them like the game's camera.
    /// </summary>
    public static class VfxPreview
    {
        [Serializable] public class ReferenceLight { public string prefab, light; public FlickerSpec flicker; }
        [Serializable] public class ReferenceLights { public ReferenceLight[] lights = new ReferenceLight[0]; }

        public static void Render(EffectSpec spec, string prefab, ReferenceLights referenceLights, string folder)
        {
            VfxEmulation.Forget();
            PreviewSpec p = spec.preview;
            Camera camera = VfxStage.Build(p);
            var roots = new List<GameObject>();
            var lights = new List<VfxLighting.Tracked>();
            if (!string.IsNullOrEmpty(prefab))
                roots.Add(Place(prefab, Vector3.up * p.lift, lights, l => spec.lights.FirstOrDefault(s => s.name == l.name)?.flicker));
            for (int i = 0; i < p.references.Length; i++)
            {
                string reference = p.references[i];
                Vector3 at = VfxCurves.Vector(p.reference_offset) * (i + (string.IsNullOrEmpty(prefab) ? 0 : 1)) + Vector3.up * p.lift;
                roots.Add(Place(reference, at, lights, l => Flicker(referenceLights, reference, l)));
            }
            Directory.CreateDirectory(folder);
            int frames = Mathf.CeilToInt(p.seconds * p.fps);
            Capture(camera, roots, lights, p, frames, folder);
            File.WriteAllText(Path.Combine(folder, "frames.json"),
                $"{{\"width\": {p.width}, \"height\": {p.height}, \"fps\": {p.fps}, \"frames\": {frames}}}");
        }

        private static GameObject Place(string path, Vector3 at, List<VfxLighting.Tracked> lights, Func<Light, FlickerSpec> flicker)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException("no prefab", path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.transform.position = at;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                Emulate(renderer);
            foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            }
            lights.AddRange(VfxLighting.Track(instance, flicker));
            return instance;
        }

        private static void Emulate(Renderer renderer)
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(VfxEmulation.For).ToArray();
            if (renderer is ParticleSystemRenderer particles && particles.trailMaterial != null)
                particles.trailMaterial = VfxEmulation.For(particles.trailMaterial);
        }

        private static FlickerSpec Flicker(ReferenceLights table, string prefab, Light light) =>
            table?.lights.FirstOrDefault(r => prefab.EndsWith(r.prefab) && r.light == light.name)?.flicker;

        private static void Capture(Camera camera, List<GameObject> roots, List<VfxLighting.Tracked> lights, PreviewSpec p,
                                    int frames, string folder)
        {
            var systems = roots.SelectMany(TopSystems).ToList();
            foreach (ParticleSystem ps in systems)
                ps.Simulate(Mathf.Max(0f, p.warmup), true, true, true);
            float dt = 1f / p.fps, time = p.warmup;
            var target = new RenderTexture(p.width, p.height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var image = new Texture2D(p.width, p.height, TextureFormat.RGBAHalf, false, true);
            for (int frame = 0; frame < frames; frame++)
            {
                if (frame > 0)
                    Step(systems, p, dt, ref time);
                VfxLighting.Apply(lights, p.repeat > 0f ? time % p.repeat : time);
                Grab(camera, target, image);
                File.WriteAllBytes(Path.Combine(folder, $"frame_{frame:0000}.half"), image.GetRawTextureData());
            }
            int alive = roots.SelectMany(r => r.GetComponentsInChildren<ParticleSystem>(true)).Sum(ps => ps.particleCount);
            Log.Info($"vfx preview: {frames} frames, {alive} particles alive at the end");
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
        }

        /// <summary>One frame on; a one-shot effect with a repeat time starts over when it comes round.</summary>
        private static void Step(List<ParticleSystem> systems, PreviewSpec p, float dt, ref float time)
        {
            time += dt;
            bool restart = p.repeat > 0f && Mathf.FloorToInt(time / p.repeat) != Mathf.FloorToInt((time - dt) / p.repeat);
            foreach (ParticleSystem ps in systems)
            {
                if (restart)
                    ps.Simulate(0f, true, true, true);
                else
                    ps.Simulate(dt, true, false, true);
            }
        }

        /// <summary>The systems not under another system: Simulate with children steps each whole tree once.</summary>
        private static IEnumerable<ParticleSystem> TopSystems(GameObject root) =>
            root.GetComponentsInChildren<ParticleSystem>(true)
                .Where(ps => ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null);

        private static void Grab(Camera camera, RenderTexture target, Texture2D image)
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
        }
    }
}
