using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// Builds the Rime Giant's bundle for Elite Creatures Reborn, one per platform:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.RimeGiant.RimeBuild.Run
    ///         -workshopFit &lt;fit.json&gt; -workshopOut &lt;folder&gt; [-workshopPreview &lt;folder&gt;]
    /// The Blender parts (staged in Assets/Bundles/ecr_rimegiant by assets/ecr_rimegiant/build.ps1) become prefabs, the
    /// kit hangs the plates and the crust on the game's Troll's bones where the fit measured them, and the kit and the
    /// boulder go into ecr_rimegiant.windows and ecr_rimegiant.linux. Nothing from Assets/Reference may go in. With
    /// -workshopPreview it also renders stills and the frames of a short video (run without -nographics for that).
    /// </summary>
    public static class RimeBuild
    {
        public const string Bundle = "ecr_rimegiant";
        public const string Folder = "Assets/Bundles/" + Bundle;
        public const string Boulder = "ecr_rime_boulder";
        private static readonly (BuildTarget target, string suffix)[] Targets =
            { (BuildTarget.StandaloneWindows64, "windows"), (BuildTarget.StandaloneLinux64, "linux") };

        public static void Run()
        {
            int code = 1;
            try
            {
                var fit = RimeFitData.Read(SlingerStage.Argument("-workshopFit"));
                string[] assets = Build(fit);
                Bundles(assets, SlingerStage.Argument("-workshopOut"));
                if (Environment.GetCommandLineArgs().Contains("-workshopPreview"))
                    RimePreview.Render(SlingerStage.Argument("-workshopPreview"), fit);
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("rime giant build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        public static string PartPrefab(string asset) => $"{Folder}/{asset}/{asset}.prefab";

        private static string[] Build(RimeFitData fit)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string part in Directory.GetDirectories(Folder))
                PrefabBuilder.Build(part.Replace('\\', '/'));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var troll = RimeReference.Troll();
            string kit = RimeKit.Build(troll, new RimePoser(troll), fit, Folder);
            UnityEngine.Object.DestroyImmediate(troll);
            return new[] { kit, FlatBoulder() };
        }

        /// <summary>The boulder's prefab as one object: the mesh on the root, the pivot in the middle of the ice.</summary>
        private static string FlatBoulder()
        {
            string path = PartPrefab(Boulder);
            var boulder = (GameObject)UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            boulder.name = Boulder;
            RimeKit.Flatten(boulder);
            PrefabUtility.SaveAsPrefabAsset(boulder, path);
            Log.Info($"boulder {path}: size {boulder.GetComponent<MeshRenderer>().bounds.size:F2}, centre {boulder.GetComponent<MeshRenderer>().bounds.center:F2}");
            UnityEngine.Object.DestroyImmediate(boulder);
            return path;
        }

        private static void Bundles(string[] assets, string outFolder)
        {
            string[] leaked = AssetDatabase.GetDependencies(assets, true).Where(a => a.StartsWith(ReferenceAssets.Folder)).ToArray();
            if (leaked.Length > 0)
                throw new InvalidOperationException("the bundle would carry the game's assets: " + string.Join(", ", leaked));
            var build = new AssetBundleBuild { assetBundleName = Bundle, assetNames = assets };
            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode;
            foreach (var (target, suffix) in Targets)
            {
                string folder = Path.Combine(outFolder, suffix);
                Directory.CreateDirectory(folder);
                if (BuildPipeline.BuildAssetBundles(folder, new[] { build }, options, target) == null)
                    throw new InvalidOperationException("bundle build failed for " + target);
                string file = Path.Combine(outFolder, Bundle + "." + suffix);
                File.Copy(Path.Combine(folder, Bundle), file, true);
                Log.Info($"bundle {file}: {new FileInfo(file).Length} bytes");
            }
        }
    }
}
