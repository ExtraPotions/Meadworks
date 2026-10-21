using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Workshop.GameRig
{
    /// <summary>
    /// Bundles the body prefab (and its regions mask, when the model has paint regions) for Windows and Linux: a bundle
    /// loads only on the platform it was built for, and dedicated servers run the Linux player. Fails when anything
    /// from Assets/Reference (the game's own assets, staged for the checks and previews) would go in.
    /// </summary>
    public static class GameRigBundle
    {
        private static readonly (BuildTarget target, string suffix)[] Targets =
            { (BuildTarget.StandaloneWindows64, "windows"), (BuildTarget.StandaloneLinux64, "linux") };

        public static void Build(string bundle, string[] assets, string outFolder)
        {
            string[] leaked = AssetDatabase.GetDependencies(assets, true).Where(a => a.StartsWith(ReferenceAssets.Folder)).ToArray();
            if (leaked.Length > 0)
                throw new InvalidOperationException("the bundle would carry the game's assets: " + string.Join(", ", leaked));
            GameRigReport.Check(true, "bundle: nothing from Assets/Reference among " + AssetDatabase.GetDependencies(assets, true).Length + " dependencies");
            var build = new AssetBundleBuild { assetBundleName = bundle, assetNames = assets };
            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode;
            foreach (var (target, suffix) in Targets)
            {
                string folder = Path.Combine(outFolder, suffix);
                Directory.CreateDirectory(folder);
                if (BuildPipeline.BuildAssetBundles(folder, new[] { build }, options, target) == null)
                    throw new InvalidOperationException("bundle build failed for " + target);
                string file = Path.Combine(outFolder, bundle + "." + suffix);
                File.Copy(Path.Combine(folder, bundle), file, true);
                GameRigReport.Line($"bundle {Path.GetFileName(file)}: {new FileInfo(file).Length / 1024} KB");
            }
            GameRigReport.Line("bundle contents: " + string.Join(", ", assets.Select(Path.GetFileName)));
        }
    }
}
