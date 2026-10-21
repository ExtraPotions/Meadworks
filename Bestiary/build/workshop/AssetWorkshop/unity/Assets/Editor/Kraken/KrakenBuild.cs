using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Kraken
{
    /// <summary>
    /// Builds Elite Creatures Pack's kraken bundle, one per platform:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Kraken.KrakenBuild.Run
    ///         -workshopOut &lt;folder&gt; [-workshopPreview &lt;folder&gt;]
    /// assets/ecp_kraken/build.ps1 stages Blender's output (the two models as JSON, the baked textures, the eye and ink
    /// textures) in Assets/Bundles/ecp_kraken. This builds the prefabs ecp_kraken_tentacle and ecp_kraken_head, checks
    /// them against the contract (KrakenCheck; any break fails the build), writes report.txt and bundles the two prefabs
    /// and the four ink textures as ecp_kraken.windows and ecp_kraken.linux. Nothing from Assets/Reference may go in.
    /// With -workshopPreview it renders stills (run without -nographics for that).
    /// </summary>
    public static class KrakenBuild
    {
        public const string Bundle = "ecp_kraken";
        public const string Folder = "Assets/Bundles/" + Bundle;
        private static readonly (BuildTarget target, string suffix)[] Targets =
            { (BuildTarget.StandaloneWindows64, "windows"), (BuildTarget.StandaloneLinux64, "linux") };

        public static void Run()
        {
            int code = 1;
            try
            {
                string outFolder = SlingerStage.Argument("-workshopOut");
                KrakenReport.Begin();
                string[] prefabs = Build();
                Verify(prefabs[0], prefabs[1]);
                Bundles(prefabs.Concat(Inks()).ToArray(), outFolder);
                KrakenReport.Save(Path.Combine(outFolder, "report.txt"));
                if (Environment.GetCommandLineArgs().Contains("-workshopPreview"))
                {
                    KrakenPreview.Render(SlingerStage.Argument("-workshopPreview"), Load(prefabs[0]), Load(prefabs[1]));
                    KrakenReport.Save(Path.Combine(outFolder, "report.txt"));
                }
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("kraken build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        public static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static string[] Inks() => KrakenContract.Inks.Select(n => Folder + "/" + n + ".png").ToArray();

        private static string[] Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string ink in Inks())
            {
                var texture = KrakenAssets.Overlay(ink);
                KrakenReport.Line($"texture {texture.name}: {texture.width}x{texture.height} {texture.format}, sRGB, no mipmaps, clamped");
            }
            var tentacle = KrakenModel.Read(Folder + "/" + KrakenContract.Tentacle + ".json");
            var head = KrakenModel.Read(Folder + "/" + KrakenContract.Head + ".json");
            // Culling boxes about the root bone, big enough for any pose: a tentacle reaches 8 m from its base; the head
            // spans its column (y -5) to the mantle (y 6.5), leans up to 40 degrees either way on kh_neck, and its column
            // bends up to 3 x 30 degrees any way on kh_body_1..3 (KrakenHeadCheck.Culling tries those poses).
            string t = KrakenPrefab.Build(Folder, tentacle, new Bounds(Vector3.zero, Vector3.one * 20f));
            string h = KrakenPrefab.Build(Folder, head, new Bounds(new Vector3(0f, 1.1f, -0.2f), new Vector3(10.6f, 13.2f, 11.2f)));
            return new[] { t, h };
        }

        private static void Verify(string tentaclePath, string headPath)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject tentacle = Spawn(tentaclePath), head = Spawn(headPath);
            KrakenCheck.Run(tentacle, head, Folder);
            KrakenReport.Prefab(tentacle);
            KrakenReport.Radii(tentacle);
            KrakenReport.Prefab(head);
            UnityEngine.Object.DestroyImmediate(tentacle);
            UnityEngine.Object.DestroyImmediate(head);
        }

        public static GameObject Spawn(string path)
        {
            var prefab = Load(path);
            var copy = UnityEngine.Object.Instantiate(prefab);
            copy.name = prefab.name;
            foreach (var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.forceMatrixRecalculationPerRender = true;   // batch mode skins once and keeps it otherwise
            return copy;
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
                KrakenReport.Bundle(file);
            }
            KrakenReport.Line("bundle contents: " + string.Join(", ", assets.Select(Path.GetFileNameWithoutExtension)));
        }
    }
}
