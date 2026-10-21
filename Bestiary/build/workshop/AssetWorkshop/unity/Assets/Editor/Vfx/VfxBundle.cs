using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Workshop.Vfx
{
    /// <summary>
    /// Builds a mod's effect bundle: each effect's prefab and its _parts.txt, one bundle per platform (a bundle only loads
    /// on the platform it was built for, and dedicated servers run the Linux player). Fails if anything from the game
    /// (Assets/Reference) or the workshop's preview shaders (Assets/Editor) would go in: the bundle holds our textures,
    /// meshes and placeholder materials only, and the mod dresses them in the game's shaders at runtime.
    /// </summary>
    public static class VfxBundle
    {
        private static readonly (BuildTarget target, string suffix)[] Targets =
            { (BuildTarget.StandaloneWindows64, "windows"), (BuildTarget.StandaloneLinux64, "linux") };

        public static void Build(string bundle, string[] assets, string outFolder)
        {
            string[] dependencies = AssetDatabase.GetDependencies(assets, true);
            string[] leaked = dependencies.Where(a => a.StartsWith("Assets/Reference") || a.StartsWith("Assets/Editor")).ToArray();
            if (leaked.Length > 0)
                throw new InvalidOperationException("the bundle would carry the game's or the preview's assets: " + string.Join(", ", leaked));
            var build = new AssetBundleBuild { assetBundleName = bundle, assetNames = assets };
            foreach (var (target, suffix) in Targets)
                For(target, suffix, build, outFolder);
            Log.Info($"vfx bundle {bundle}: {assets.Length} assets, {dependencies.Length} with dependencies, none from the game");
        }

        private static void For(BuildTarget target, string suffix, AssetBundleBuild build, string outFolder)
        {
            string folder = Path.Combine(outFolder, suffix);
            Directory.CreateDirectory(folder);
            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode;
            if (BuildPipeline.BuildAssetBundles(folder, new[] { build }, options, target) == null)
                throw new InvalidOperationException("bundle build failed for " + target);
            string file = Path.Combine(outFolder, build.assetBundleName + "." + suffix);
            File.Copy(Path.Combine(folder, build.assetBundleName), file, true);
            Log.Info($"vfx bundle {file}: {new FileInfo(file).Length} bytes");
        }
    }
}
