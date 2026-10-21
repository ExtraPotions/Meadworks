using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Builds the Skeleton Crossbowman's bundle for Elite Creatures Pack, one per platform:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Crossbow.XbowBuild.Run
    ///         -workshopOut &lt;folder&gt; [-workshopPreview &lt;folder&gt;]
    /// The three Blender parts (staged in Assets/Bundles/ecp_crossbowman by assets/ecp_crossbowman/build.ps1) become
    /// prefabs; the two shot clips and the three carry clips (<see cref="XbowCarry"/>) are authored on the game's
    /// Skeleton through its own Animator, twice (the second time with the right fingertips measured on the first as the
    /// Animator plays it); <see cref="XbowCheck"/> plays them back and logs how well crossbow, hands and bolts meet; the
    /// kit is measured on the same skeleton; the players' Bone Crossbow gets its two held models (<see cref="XbowItemModels"/>)
    /// and its icon; all of it goes into ecp_crossbowman.windows and .linux. Nothing from Assets/Reference may go in.
    /// </summary>
    public static class XbowBuild
    {
        public const string Bundle = "ecp_crossbowman";
        public const string Folder = "Assets/Bundles/" + Bundle;
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
                    XbowPreview.Render(SlingerStage.Argument("-workshopPreview"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("crossbowman build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        public static string ClipPath(string name) => Folder + "/" + name + ".anim";

        /// <summary>
        /// The parts the mod loads by name besides the kit: the bolt (for the projectile), and the crossbow, its string and
        /// the crossbow's inventory icon (a sprite) for the players' Bone Crossbow.
        /// </summary>
        private static string[] Parts()
        {
            string Prefab(string asset) => $"{Folder}/{asset}/{asset}.prefab";
            string icon = PrefabBuilder.Icon(Folder + "/ecp_xbow_crossbow");
            if (icon == null)
                throw new InvalidOperationException("no ecp_xbow_crossbow_icon.png staged; run assets/ecp_xbow_crossbow/icon.py");
            string boltIcon = PrefabBuilder.Icon(Folder + "/" + XbowBolt.Asset);
            if (boltIcon == null)
                throw new InvalidOperationException("no ecp_xbow_bolt_icon.png staged; run assets/ecp_crossbowman/build.ps1");
            return new[] { Prefab(XbowBolt.Asset), Prefab("ecp_xbow_crossbow"), Prefab("ecp_xbow_string"), icon, boltIcon };
        }

        private static string[] Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string part in Directory.GetDirectories(Folder))
                PrefabBuilder.Build(part.Replace('\\', '/'));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject skeleton = XbowReference.Skeleton();
            XbowGrip grip = FirstGrip(skeleton);
            Author(skeleton, grip);
            Measure(skeleton, grip);
            Author(skeleton, grip);
            XbowCheck.Run(skeleton, grip);
            new XbowPoser(skeleton).Pose("Idle", 0f);
            string kit = XbowKit.Build(skeleton, grip, Folder);
            UnityEngine.Object.DestroyImmediate(skeleton);
            return new[] { kit }.Concat(Parts()).Concat(XbowItemModels.Build(Folder)).Concat(ClipNames().Select(ClipPath)).ToArray();
        }

        private static XbowGrip FirstGrip(GameObject skeleton)
        {
            var grip = new XbowGrip();
            var stance = new XbowStance(skeleton, new XbowPoser(skeleton), grip);
            stance.Rest();
            grip.MeasureBow(stance);
            grip.MeasureCarry(stance);
            grip.GuessPinch(stance);
            grip.Quiver = XbowGrip.QuiverOn(skeleton);
            Log.Info($"crossbow in the left fist: {grip.Bow.position:F4} {grip.Bow.rotation.eulerAngles:F1}");
            return grip;
        }

        private static void Author(GameObject skeleton, XbowGrip grip)
        {
            var poser = new XbowPoser(skeleton);
            var stance = new XbowStance(skeleton, poser, grip);
            var author = new XbowAuthor(skeleton, stance);
            XbowCarry.Write(skeleton, grip, poser, author.Curled);
            author.Write(XbowClips.AimName, XbowClips.AimKeys(), XbowClips.AimLength, new AnimationEvent[0], ClipPath(XbowClips.AimName));
            var shot = new AnimationEvent { time = XbowClips.Fire, functionName = "OnAttackTrigger" };
            author.Write(XbowClips.FireName, XbowClips.FireKeys(), XbowClips.FireLength, new[] { shot }, ClipPath(XbowClips.FireName));
            author.Write(XbowClips.PlayerReloadName, XbowClips.PlayerReloadKeys(), XbowClips.PlayerReloadLength, new AnimationEvent[0], ClipPath(XbowClips.PlayerReloadName));
            author.Write(XbowClips.PlayerDoneName, XbowClips.PlayerDoneKeys(), XbowClips.PlayerDoneLength, new AnimationEvent[0], ClipPath(XbowClips.PlayerDoneName));
        }

        /// <summary>The pinch as the played clip curls the fingers, at the lay (<see cref="XbowGrip.MeasurePinch"/>).</summary>
        private static void Measure(GameObject skeleton, XbowGrip grip)
        {
            XbowPoser played = Played(skeleton);
            played.Pose(XbowClips.FireName, XbowClips.Lay);
            grip.MeasurePinch(new XbowStance(skeleton, played, grip));
            Log.Info($"pinch measured: {grip.PinchOffset:F4} {grip.PinchRotation.eulerAngles:F1}");
        }

        /// <summary>Every clip the bundle carries: the two shot clips and the three carry clips.</summary>
        public static string[] ClipNames() =>
            new[] { XbowClips.AimName, XbowClips.FireName, XbowClips.PlayerReloadName, XbowClips.PlayerDoneName }
                .Concat(XbowCarry.Clips.Select(c => c.name)).ToArray();

        /// <summary>A poser with the authored clips as states, to see them as the game's Animator plays them.</summary>
        public static XbowPoser Played(GameObject skeleton) =>
            new XbowPoser(skeleton, ClipNames().Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(n))).ToArray());

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
