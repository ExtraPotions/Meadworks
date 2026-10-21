using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>
    /// Builds an effect prefab from its spec in a staging folder: an empty root, one child per particle system (under
    /// the system its spec names as parent), the lights, and beside it &lt;name&gt;_parts.txt, the game components the mod
    /// adds at runtime (LightFlicker, LightLod, TimedDestruction, CamShaker) since the bundle cannot carry the game's
    /// scripts. Returns the prefab's path.
    /// </summary>
    public static class VfxPrefab
    {
        public static string Build(string folder, EffectSpec spec)
        {
            var textures = spec.textures.ToDictionary(t => t.name, t => VfxMaterials.Texture(folder, t));
            var materials = spec.materials.ToDictionary(m => m.name,
                m => VfxMaterials.Placeholder(folder, m, string.IsNullOrEmpty(m.texture) ? null : textures[m.texture]));
            var meshes = spec.meshes.ToDictionary(m => m.name, m => VfxMeshes.Make(folder, m));
            var root = new GameObject(spec.name);
            var systems = new Dictionary<string, ParticleSystem>();
            foreach (SystemSpec s in spec.systems)
                systems[s.name] = System(root.transform, systems, s, name => materials[name], name => meshes[name]);
            foreach (SystemSpec s in spec.systems)
                VfxLife.Link(systems[s.name], s, name => systems.TryGetValue(name, out var found) ? found : null);
            foreach (LightSpec l in spec.lights)
                MakeLight(Parent(root.transform, systems, l.parent), l);
            string path = folder + "/" + spec.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            File.WriteAllText(folder + "/" + spec.name + "_parts.txt", Parts(spec));
            AssetDatabase.ImportAsset(folder + "/" + spec.name + "_parts.txt", ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        private static ParticleSystem System(Transform root, Dictionary<string, ParticleSystem> built, SystemSpec s,
                                             Func<string, Material> materials, Func<string, Mesh> meshes)
        {
            var node = new GameObject(s.name);
            node.transform.SetParent(Parent(root, built, s.parent), false);
            node.transform.localPosition = VfxCurves.Vector(s.position);
            node.transform.localEulerAngles = VfxCurves.Vector(s.euler);
            node.transform.localScale = VfxCurves.Vector(s.scale);
            var ps = node.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            VfxMain.Apply(ps, s, meshes);
            VfxMotion.Apply(ps, s);
            VfxLife.Apply(ps, s);
            VfxRender.Apply(node.GetComponent<ParticleSystemRenderer>(), s.renderer, materials, meshes);
            return ps;
        }

        private static Transform Parent(Transform root, Dictionary<string, ParticleSystem> built, string parent) =>
            string.IsNullOrEmpty(parent) ? root
            : built.TryGetValue(parent, out var system) ? system.transform
            : throw new InvalidOperationException("parent " + parent + " must come before its children in the spec");

        private static void MakeLight(Transform parent, LightSpec l)
        {
            var node = new GameObject(l.name);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = VfxCurves.Vector(l.position);
            var light = node.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = VfxCurves.Colour(l.colour);
            light.intensity = l.intensity;
            light.range = l.range;
            light.shadows = l.shadows == "soft" ? LightShadows.Soft : l.shadows == "hard" ? LightShadows.Hard : LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
        }

        /// <summary>
        /// One line per game component: "path|Component|field=value;field=value", path from the root ("" is the root),
        /// fields by the game's own names. BundlePrefabs' BundleEffects adds them when the mod prepares the effect.
        /// </summary>
        public static string Parts(EffectSpec spec)
        {
            var lines = new List<string>();
            foreach (LightSpec l in spec.lights)
            {
                string path = string.IsNullOrEmpty(l.parent) ? l.name : Path(spec, l.parent) + "/" + l.name;
                if (l.flicker.enabled)
                    lines.Add(Line(path, "LightFlicker", ("m_flickerIntensity", l.flicker.intensity), ("m_flickerSpeed", l.flicker.speed),
                        ("m_movement", l.flicker.movement), ("m_ttl", l.flicker.ttl), ("m_fadeDuration", l.flicker.fade),
                        ("m_fadeInDuration", l.flicker.fade_in)));
                if (l.lod.enabled)
                    lines.Add(Line(path, "LightLod", ("m_lightLod", 1), ("m_lightDistance", l.lod.distance), ("m_shadowLod", 0),
                        ("m_shadowDistance", l.lod.shadow_distance)));
            }
            if (spec.timeout > 0)
                lines.Add(Line("", "TimedDestruction", ("m_timeout", spec.timeout), ("m_triggerOnAwake", 1)));
            if (spec.shake.enabled)
                lines.Add(Line("", "CamShaker", ("m_strength", spec.shake.strength), ("m_range", spec.shake.range),
                    ("m_delay", spec.shake.delay), ("m_continous", spec.shake.continuous ? 1 : 0),
                    ("m_continousDuration", spec.shake.continuous_duration), ("m_localOnly", spec.shake.local_only ? 1 : 0)));
            return string.Join("\n", lines) + "\n";
        }

        private static string Path(EffectSpec spec, string system)
        {
            SystemSpec s = spec.systems.First(x => x.name == system);
            return string.IsNullOrEmpty(s.parent) ? s.name : Path(spec, s.parent) + "/" + s.name;
        }

        private static string Line(string path, string component, params (string key, float value)[] fields) =>
            path + "|" + component + "|" + string.Join(";", fields.Select(f => f.key + "=" + f.value.ToString(CultureInfo.InvariantCulture)));
    }
}
