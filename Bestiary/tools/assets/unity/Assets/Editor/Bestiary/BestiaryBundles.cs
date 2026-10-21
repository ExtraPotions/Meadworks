// Bestiary bundle build entry points. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bestiary.Build
{
    /// <summary>
    /// Batch-mode entry points of Bestiary's Unity build project (tools/assets/unity_build.py stages it under
    /// build/unity from EliteCreaturesPack's AssetWorkshop at the pin plus these files):
    ///   Unity -batchmode -projectPath build/unity -executeMethod Bestiary.Build.BestiaryBundles.&lt;Bundle&gt;
    ///         -bestiaryBundle &lt;name&gt; -bestiaryOut &lt;folder&gt; -bestiaryPlatforms windows,linux
    /// Every bundle is written as &lt;out&gt;/&lt;name&gt;.&lt;platform&gt;, with &lt;out&gt;/&lt;name&gt;.report.txt listing what
    /// went in and which platforms were built or unavailable. Nothing of the game's is read: the build fails if
    /// Assets/Reference (the workshop's local game export) exists or if any bundled asset depends on it.
    /// </summary>
    public static class BestiaryBundles
    {
        public const string BundlesFolder = "Assets/Bundles";
        private const string ReferenceFolder = "Assets/Reference";
        private static readonly List<string> Report = new List<string>();

        public static void Generic() => Run(GenericBuild);
        public static void Kraken() => Run(BestiaryCreatures.Kraken);
        public static void Mimic() => Run(BestiaryCreatures.Mimic);
        public static void Slinger() => Run(BestiaryKits.Slinger);
        public static void Crossbowman() => Run(BestiaryKits.Crossbowman);
        public static void Headsman() => Run(BestiaryHeadsman.Build);
        public static void RimeGiant() => Run(BestiaryRime.Build);

        private static void Run(Func<string, string[]> build)
        {
            int code = 1;
            string bundle = "?";
            try
            {
                if (Directory.Exists(ReferenceFolder))
                    throw new InvalidOperationException(ReferenceFolder + " exists: Bestiary never uses a game export");
                bundle = Argument("-bestiaryBundle");
                Report.Clear();
                Line("bundle " + bundle);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string[] assets = build(bundle);
                code = Write(bundle, assets, Argument("-bestiaryOut"), Platforms()) ? 0 : 1;
            }
            catch (Exception e)
            {
                BLog.Error($"{bundle} build failed: {e}");
            }
            EditorApplication.Exit(code);
        }

        /// <summary>Every staged part folder becomes a prefab (and its icon a sprite): the kraken loot and the arsenal.</summary>
        private static string[] GenericBuild(string bundle)
        {
            string[] parts = PartFolders(bundle);
            var assets = new List<string>();
            foreach (string folder in parts)
            {
                assets.Add(Workshop.PrefabBuilder.Build(folder));
                string icon = Workshop.PrefabBuilder.Icon(folder);
                if (icon != null)
                    assets.Add(icon);
            }
            return assets.ToArray();
        }

        /// <summary>The staged part folders of a bundle (folders starting with '_' hold Bestiary's own inputs).</summary>
        public static string[] PartFolders(string bundle) =>
            Directory.GetDirectories(BundlesFolder + "/" + bundle).Select(d => d.Replace('\\', '/'))
                .Where(d => !Path.GetFileName(d).StartsWith("_")).OrderBy(d => d, StringComparer.Ordinal).ToArray();

        private static bool Write(string bundle, string[] assets, string outFolder, string[] platforms)
        {
            string[] dependencies = AssetDatabase.GetDependencies(assets, true);
            string[] leaked = dependencies.Where(a => a.StartsWith(ReferenceFolder)).ToArray();
            if (leaked.Length > 0)
                throw new InvalidOperationException("the bundle would carry the game's assets: " + string.Join(", ", leaked));
            foreach (string asset in assets.OrderBy(a => a, StringComparer.Ordinal))
                Line("asset " + asset);
            Line($"dependencies {dependencies.Length}");
            var build = new AssetBundleBuild { assetBundleName = bundle, assetNames = assets };
            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode;
            bool any = false;
            foreach (string platform in platforms)
            {
                BuildTarget target = platform == "windows" ? BuildTarget.StandaloneWindows64
                    : platform == "linux" ? BuildTarget.StandaloneLinux64
                    : throw new ArgumentException("unknown platform " + platform);
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                {
                    Line($"platform {platform} unavailable: {target} build support is not installed in this editor");
                    BLog.Error($"{bundle}: {target} build support is not installed in this editor");
                    continue;
                }
                string folder = Path.Combine(outFolder, platform);
                Directory.CreateDirectory(folder);
                if (BuildPipeline.BuildAssetBundles(folder, new[] { build }, options, target) == null)
                    throw new InvalidOperationException("bundle build failed for " + target);
                string file = Path.Combine(outFolder, bundle + "." + platform);
                File.Copy(Path.Combine(folder, bundle), file, true);
                Line($"platform {platform} built: {new FileInfo(file).Length} bytes");
                BLog.Info($"bundle {file}: {new FileInfo(file).Length} bytes");
                any = true;
            }
            File.WriteAllLines(Path.Combine(outFolder, bundle + ".report.txt"), Report);
            return any;
        }

        public static void Line(string line)
        {
            Report.Add(line);
            BLog.Info(line);
        }

        private static string[] Platforms() =>
            Argument("-bestiaryPlatforms").Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();

        public static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("missing argument " + name);
            return args[index + 1];
        }
    }

    /// <summary>BESTIARY-prefixed log lines without stack traces, so unity_build.py can pick them out of the log.</summary>
    internal static class BLog
    {
        public static void Info(string message) =>
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "BESTIARY {0}", message);

        public static void Error(string message) =>
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "BESTIARY ERROR {0}", message);
    }
}
