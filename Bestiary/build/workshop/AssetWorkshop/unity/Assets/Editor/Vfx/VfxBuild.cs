using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>
    /// Batch-mode entry point for effects; AssetWorkshop/vfx/build.py runs it:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Vfx.VfxBuild.Run
    ///         -workshopVfx &lt;folder&gt;[;&lt;folder&gt;...] [-workshopBundle &lt;name&gt; -workshopOut &lt;folder&gt;]
    ///         [-workshopPreview] [-workshopReferenceLights &lt;json&gt;]
    /// Each folder holds an effect.json (vfx/spec.py) and its textures. The effects are staged in
    /// Assets/Bundles/vfx_&lt;bundle&gt;/&lt;effect&gt; (gitignored) and built into prefabs; with -workshopBundle they go into one
    /// bundle per platform; with -workshopPreview each is rendered into &lt;folder&gt;/frames. Run without -nographics
    /// when previewing: rendering needs the GPU.
    /// </summary>
    public static class VfxBuild
    {
        public static void Run()
        {
            int code = 1;
            try
            {
                Build();
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("vfx build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Build()
        {
            string bundle = Optional("-workshopBundle");
            string stageRoot = "Assets/Bundles/vfx_" + (bundle ?? "preview");
            var assets = new List<string>();
            var built = new List<(EffectSpec spec, string prefab, string folder)>();
            foreach (string folder in Argument("-workshopVfx").Split(';'))
            {
                EffectSpec spec = EffectSpec.Load(Path.Combine(folder, "effect.json"));
                string stage = Stage(folder, stageRoot, spec);
                string prefab = spec.systems.Length + spec.lights.Length > 0 ? VfxPrefab.Build(stage, spec) : null;
                if (prefab != null)
                    assets.AddRange(new[] { prefab, stage + "/" + spec.name + "_parts.txt" });
                built.Add((spec, prefab, folder));
                Log.Info($"vfx {spec.name}: {spec.systems.Length} systems, {spec.lights.Length} lights -> {prefab ?? "(reference only)"}");
            }
            if (bundle != null)
                VfxBundle.Build(bundle, assets.ToArray(), Argument("-workshopOut"));
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-workshopPreview") >= 0)
                Preview(built);
        }

        private static void Preview(List<(EffectSpec spec, string prefab, string folder)> built)
        {
            string table = Optional("-workshopReferenceLights");
            var lights = table != null ? JsonUtility.FromJson<VfxPreview.ReferenceLights>(File.ReadAllText(table)) : null;
            foreach (var (spec, prefab, folder) in built)
            {
                VfxPreview.Render(spec, prefab, lights, Path.Combine(folder, "frames"));
                Log.Info($"vfx preview {spec.name}: {Path.Combine(folder, "frames")}");
            }
        }

        /// <summary>A fresh staging folder with the effect's texture files, imported.</summary>
        private static string Stage(string folder, string root, EffectSpec spec)
        {
            string stage = root + "/" + spec.name;
            if (AssetDatabase.IsValidFolder(stage))
                AssetDatabase.DeleteAsset(stage);
            Directory.CreateDirectory(stage);
            foreach (TextureSpec t in spec.textures)
                File.Copy(Path.Combine(folder, t.file), Path.Combine(stage, t.file), true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return stage;
        }

        private static string Argument(string name) =>
            Optional(name) ?? throw new ArgumentException("missing argument " + name);

        private static string Optional(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
