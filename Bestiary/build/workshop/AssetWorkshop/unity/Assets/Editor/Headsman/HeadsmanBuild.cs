using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Slinger;

namespace Workshop.Headsman
{
    /// <summary>
    /// Builds the headsman's animations (working name for Elite Creatures Pack's greataxe skeleton boss):
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Headsman.HeadsmanBuild.Run
    ///         [-workshopOut &lt;bake folder&gt;] [-workshopStills &lt;folder&gt;] [-workshopBundle &lt;folder&gt;]
    /// The bone greataxe (staged in Assets/Bundles/ecp_headsman by assets/ecp_headsman/build.ps1) becomes a prefab; the
    /// fists are measured on the game's Skeleton; the carry idle, walk and run and the six attacks are authored on it
    /// as humanoid clips (<see cref="HeadsmanAuthor"/>) into Assets/Bundles/ecp_headsman; <see cref="HeadsmanCheck"/>
    /// plays them back; then contact sheets (<see cref="HeadsmanStills"/>), the mod's bundle (<see cref="HeadsmanBundle"/>)
    /// and the bake of the whole sequence for Blender (<see cref="HeadsmanBake"/>). Nothing from Assets/Reference may go into anything shipped.
    /// </summary>
    public static class HeadsmanBuild
    {
        public const string Bundle = "ecp_headsman";
        public const string Folder = "Assets/Bundles/" + Bundle;

        public static void Run()
        {
            int code = 1;
            try
            {
                Build();
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("headsman build failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        public static string ClipPath(string name) => Folder + "/" + name + ".anim";

        /// <summary>The six attacks, in the order the preview plays them.</summary>
        public static HeadsmanMove[] Moves() => new[]
        {
            HeadsmanMelee.Slam(), HeadsmanMelee.Scrape(), HeadsmanMelee.Spin(),
            HeadsmanRanged.Hurl(), HeadsmanRanged.SpinThrow(), HeadsmanRanged.Rear(),
        };

        private static void Build()
        {
            HeadsmanAxe.Stage();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject skeleton = XbowReference.Skeleton();
            var poser = new XbowPoser(skeleton);
            var grip = new HeadsmanGrip();
            var stance = new HeadsmanStance(skeleton, poser, grip);
            Animator animator = XbowReference.Animator(skeleton);
            var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            var fingers = new HeadsmanFingers(stance, handler);
            stance.LeftWrist = HeadsmanWrist.Neutral(stance, handler, "Left");
            stance.RightWrist = HeadsmanWrist.Neutral(stance, handler, "Right");
            fingers.CloseOnRest(stance, handler);
            grip.Measure(stance);
            HeadsmanRest.Measure(stance);
            HeadsmanMove[] moves = Moves();
            Author(skeleton, stance, fingers, poser, moves);
            XbowPoser played = Played(skeleton, moves);
            HeadsmanCheck.Run(skeleton, grip, moves, played);
            string[] args = Environment.GetCommandLineArgs();
            if (args.Contains("-workshopStills"))
                HeadsmanStills.Render(SlingerStage.Argument("-workshopStills"), skeleton, grip, moves, played);
            if (args.Contains("-workshopBundle"))
                HeadsmanBundle.Build(skeleton, grip, moves, SlingerStage.Argument("-workshopBundle"));
            UnityEngine.Object.DestroyImmediate(skeleton);
            if (args.Contains("-workshopOut"))
                HeadsmanBake.Run(SlingerStage.Argument("-workshopOut"), grip, moves);
        }

        private static void Author(GameObject skeleton, HeadsmanStance stance, HeadsmanFingers fingers, XbowPoser poser, HeadsmanMove[] moves)
        {
            var author = new HeadsmanAuthor(skeleton, stance, fingers);
            foreach (var (name, state) in HeadsmanAuthor.CarryClips)
                author.Carry(name, state, poser, ClipPath(name));
            foreach (HeadsmanMove move in moves)
                author.Write(move, ClipPath(move.Clip));
            AssetDatabase.SaveAssets();
        }

        /// <summary>Every clip the headsman has: the carry idle, walk and run, then the attacks.</summary>
        public static string[] ClipNames(HeadsmanMove[] moves) =>
            HeadsmanAuthor.CarryClips.Select(c => c.name).Concat(moves.Select(m => m.Clip)).ToArray();

        public static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name));

        /// <summary>A poser with the authored clips as states, to see them as the game's Animator plays them.</summary>
        public static XbowPoser Played(GameObject skeleton, HeadsmanMove[] moves) =>
            new XbowPoser(skeleton, ClipNames(moves).Select(Clip).ToArray());
    }
}
