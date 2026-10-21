using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Builds the asset bundle a mod ships for a creature: its prefab - the rig, its parts on bare placeholder materials
    /// (named like the Blender materials, no textures), the clips and the animator controller. The mod swaps in the
    /// game's own materials at runtime, so nothing of the game's goes into the bundle; the build fails if anything from
    /// Assets/Reference is pulled in. One bundle per platform, since a bundle only loads on the platform it was built
    /// for:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.CreatureBundle.Run -workshopCreature &lt;name&gt;
    ///         -workshopBundle &lt;bundle&gt; -workshopOut &lt;folder&gt;
    /// </summary>
    public static class CreatureBundle
    {
        private static readonly (BuildTarget target, string folder)[] Targets =
            { (BuildTarget.StandaloneWindows64, "windows"), (BuildTarget.StandaloneLinux64, "linux") };

        public static void Run()
        {
            int code = 1;
            try
            {
                Build(Argument("-workshopCreature"), Argument("-workshopBundle"), Argument("-workshopOut"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("creature bundle failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        public static void Build(string creature, string bundle, string outFolder)
        {
            string folder = "Assets/Creatures/" + creature;
            var info = CreatureManifest.Read(folder);
            string prefab = folder + "/" + info.asset + ".prefab";   // bare materials already: see CreatureImport
            string[] assets = AssetDatabase.GetDependencies(prefab, true);
            var leaked = assets.Where(a => a.StartsWith(ReferenceAssets.Folder)).ToArray();
            if (leaked.Length > 0)
                throw new InvalidOperationException("the bundle would carry the game's assets: " + string.Join(", ", leaked));
            var build = new AssetBundleBuild { assetBundleName = bundle, assetNames = new[] { prefab } };
            foreach (var (target, name) in Targets)
                BuildFor(target, Path.Combine(outFolder, name), build);
            Log.Info($"creature bundle {bundle}: {assets.Length} assets, none from the game");
        }

        private static void BuildFor(BuildTarget target, string folder, AssetBundleBuild build)
        {
            Directory.CreateDirectory(folder);
            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode;
            if (BuildPipeline.BuildAssetBundles(folder, new[] { build }, options, target) == null)
                throw new InvalidOperationException("bundle build failed for " + target);
            long size = new FileInfo(Path.Combine(folder, build.assetBundleName)).Length;
            Log.Info($"bundle {build.assetBundleName} for {target}: {size} bytes");
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new ArgumentException("missing " + name);
        }
    }
}
