using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Sfx
{
    /// <summary>
    /// Batch-mode entry point for a sound set's bundle. sfx/bundle.ps1 stages the set's WAVs and manifest.json in
    /// Assets/Bundles/Sfx/&lt;bundle&gt;/ and runs:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Sfx.SfxBundle.Run
    ///         -workshopBundle &lt;bundle&gt; -workshopOut &lt;folder&gt;
    /// The bundle holds the set's AudioClips only (loaded by name: bundle.LoadAsset(clip, typeof(AudioClip))), built for
    /// StandaloneWindows64 and StandaloneLinux64 as &lt;out&gt;/&lt;bundle&gt;.windows and .linux, the names
    /// BundlePrefabs.EmbeddedBundle looks for. Nothing of the game's is ever in it: SfxImport refuses unlisted files.
    /// </summary>
    public static class SfxBundle
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
                Log.Error("sfx bundle failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static int Build(string bundle, string outFolder)
        {
            string folder = "Assets/Bundles/Sfx/" + bundle;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string[] clips = SfxImport.Prepare(folder);
            var build = new AssetBundleBuild { assetBundleName = bundle, assetNames = clips };
            foreach (var (target, suffix) in Targets)
            {
                if (!BuildFor(target, suffix, build, outFolder))
                    return 1;
            }
            if (!Verify(Path.Combine(outFolder, bundle + ".windows"), clips))
                return 1;
            Log.Info($"sfx bundle {bundle}: {clips.Length} clip(s)");
            return 0;
        }

        /// <summary>Loads the Windows bundle back from disk (the editor runs on Windows) and checks that every clip
        /// loads as an AudioClip by its short name, the way BundlePrefabs.SfxPrefabs.Load asks for it.</summary>
        private static bool Verify(string file, string[] clips)
        {
            var loaded = AssetBundle.LoadFromFile(file);
            if (loaded == null)
            {
                Log.Error("sfx bundle did not load back: " + file);
                return false;
            }
            int missing = 0;
            foreach (string clip in clips.Select(Path.GetFileNameWithoutExtension))
            {
                if (loaded.LoadAsset<AudioClip>(clip) == null)
                {
                    Log.Error("sfx bundle has no audio clip " + clip);
                    missing++;
                }
            }
            loaded.Unload(true);
            Log.Info($"sfx bundle {Path.GetFileName(file)} loads back: {clips.Length - missing} of {clips.Length} clips by name");
            return missing == 0;
        }

        private static bool BuildFor(BuildTarget target, string suffix, AssetBundleBuild build, string outFolder)
        {
            string folder = Path.Combine(outFolder, "sfx_" + suffix);
            Directory.CreateDirectory(folder);
            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode;
            if (BuildPipeline.BuildAssetBundles(folder, new[] { build }, options, target) == null)
                return false;
            string file = Path.Combine(outFolder, build.assetBundleName + "." + suffix);
            File.Copy(Path.Combine(folder, build.assetBundleName), file, true);
            Log.Info($"sfx bundle {file} for {target}: {new FileInfo(file).Length} bytes");
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
}
