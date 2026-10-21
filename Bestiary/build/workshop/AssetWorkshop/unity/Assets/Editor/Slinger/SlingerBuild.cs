using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// Builds the Greydwarf Slinger's bundle for Elite Creatures Reborn, one per platform:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Slinger.SlingerBuild.Run
    ///         -workshopOut &lt;folder&gt; [-workshopPreview &lt;folder&gt;]
    /// The four Blender parts (staged in Assets/Bundles/ecr_slinger by assets/ecr_slinger/build.ps1) become prefabs, the
    /// shot is authored on the game's Greydwarf skeleton at the game's scale, the kit is measured on it, and the kit and
    /// the clip go into ecr_slinger.windows and ecr_slinger.linux. Nothing from Assets/Reference may go in. With
    /// -workshopPreview it also renders stills and the frames of a short video (run without -nographics for that).
    /// </summary>
    public static class SlingerBuild
    {
        public const string Bundle = "ecr_slinger";
        public const string Folder = "Assets/Bundles/" + Bundle;
        private const float GameArmatureScale = 50f;   // the game's Greydwarf; the model prefab has 100
        private static readonly (BuildTarget target, string suffix)[] Targets =
            { (BuildTarget.StandaloneWindows64, "windows"), (BuildTarget.StandaloneLinux64, "linux") };

        public static void Run()
        {
            int code = 1;
            try
            {
                string[] assets = Build();
                Bundles(assets, SlingerStage.Argument("-workshopOut"));
                if (Environment.GetCommandLineArgs().Contains("-workshopPreview"))
                    SlingerPreview.Render(SlingerStage.Argument("-workshopPreview"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("slinger build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        /// <summary>The Greydwarf model at the game's size: its armature at the game's scale, animator rebound to it.</summary>
        public static GameObject GameSizedGreydwarf()
        {
            var greydwarf = SlingerReference.Greydwarf();
            SlingerReference.Bone(greydwarf, "Armature.001").localScale = Vector3.one * GameArmatureScale;
            greydwarf.GetComponent<Animator>().Rebind();
            foreach (var skin in greydwarf.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.forceMatrixRecalculationPerRender = true;   // batch mode skins once and keeps it otherwise
            return greydwarf;
        }

        private static string[] Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string part in Directory.GetDirectories(Folder))
                PrefabBuilder.Build(part.Replace('\\', '/'));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var greydwarf = GameSizedGreydwarf();
            AnimationClip idle = SlingerReference.Clip("Idle");
            string clipPath = Folder + "/" + SlingClip.Name + ".anim";
            AnimationClip shot = SlingClip.Build(greydwarf, idle, SlingerReference.Clip("Throw"), clipPath);
            string kit = SlingKit.Build(greydwarf, shot, idle, Folder);
            UnityEngine.Object.DestroyImmediate(greydwarf);
            return new[] { kit, clipPath };
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
