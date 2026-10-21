using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Batch-mode entry point. build.ps1 stages each asset's Blender output in Assets/Bundles/&lt;bundle&gt;/&lt;asset&gt;/
    /// and runs: Unity -batchmode -projectPath unity -executeMethod Workshop.BundleBuild.Run
    ///           -workshopBundle &lt;bundle&gt; -workshopOut &lt;folder&gt;
    /// Every asset folder becomes one prefab, and its &lt;asset&gt;_icon.png (when there is one) a sprite of that name
    /// for the item's inventory icon; they go into one bundle per platform, written as
    /// &lt;out&gt;/&lt;bundle&gt;.windows and &lt;out&gt;/&lt;bundle&gt;.linux (the names BundlePrefabs.EmbeddedBundle looks for; a
    /// bundle only loads on the platform it was built for, and dedicated servers run the Linux player).
    /// </summary>
    public static class BundleBuild
    {
        private static readonly (BuildTarget target, string suffix)[] Targets =
            { (BuildTarget.StandaloneWindows64, "windows"), (BuildTarget.StandaloneLinux64, "linux") };

        public static void Run()
        {
            int code = 1;
            try
            {
                code = Build(Argument("-workshopBundle"), Argument("-workshopOut"));
            }
            catch (Exception e)
            {
                Log.Error("build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static int Build(string bundle, string outFolder)
        {
            string root = "Assets/Bundles/" + bundle;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string[] folders = Directory.GetDirectories(root).Select(d => d.Replace('\\', '/')).ToArray();
            string[] prefabs = folders.Select(PrefabBuilder.Build).ToArray();
            string[] icons = folders.Select(PrefabBuilder.Icon).Where(icon => icon != null).ToArray();
            // Clips authored in Unity for this bundle (not staged from assets/, so a rebuild leaves them): Assets/BundleExtras/<bundle>.
            string extras = "Assets/BundleExtras/" + bundle;
            string[] extra = Directory.Exists(extras) ? Directory.GetFiles(extras, "*.anim").Select(p => p.Replace('\\', '/')).ToArray() : new string[0];
            var build = new AssetBundleBuild { assetBundleName = bundle, assetNames = prefabs.Concat(icons).Concat(extra).ToArray() };
            foreach (var (target, suffix) in Targets)
            {
                if (!BuildFor(target, suffix, build, outFolder))
                    return 1;
            }
            Log.Info($"bundle {bundle}: {prefabs.Length} prefab(s), {icons.Length} icon(s), {extra.Length} clip(s)");
            return 0;
        }

        /// <summary>Builds into &lt;out&gt;/&lt;platform&gt;/ (Unity writes manifests there too), then copies the bundle out.</summary>
        private static bool BuildFor(BuildTarget target, string suffix, AssetBundleBuild build, string outFolder)
        {
            string folder = Path.Combine(outFolder, suffix);
            Directory.CreateDirectory(folder);
            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode;
            if (BuildPipeline.BuildAssetBundles(folder, new[] { build }, options, target) == null)
                return false;
            string file = Path.Combine(outFolder, build.assetBundleName + "." + suffix);
            File.Copy(Path.Combine(folder, build.assetBundleName), file, true);
            Log.Info($"bundle {file} for {target}: {new FileInfo(file).Length} bytes");
            return true;
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("missing argument " + name);
            return args[index + 1];
        }
    }

    /// <summary>WORKSHOP-prefixed log lines without stack traces, so build.ps1 can pick them out of the log.</summary>
    internal static class Log
    {
        public static void Info(string message) =>
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "WORKSHOP {0}", message);

        public static void Error(string message) =>
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "WORKSHOP ERROR {0}", message);
    }
}
