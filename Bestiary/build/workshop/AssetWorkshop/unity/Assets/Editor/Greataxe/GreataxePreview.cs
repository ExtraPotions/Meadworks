using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Headsman;
using Workshop.Slinger;

namespace Workshop.Greataxe
{
    /// <summary>
    /// The Executioner's Greataxe (Elite Creatures Pack's craftable weapon) in a player's hands, for Blender:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Greataxe.GreataxePreview.Run -workshopOut &lt;folder&gt;
    /// (assets/ecp_headsman/build.ps1 -Player). The game's player (reference, preview only) holds the greataxe on
    /// RightHand_Attach (<see cref="GreataxeModel"/>), a training dummy in front: each stance and movement the game
    /// plays for a Battleaxe-type weapon, a jump, then the combo (<see cref="GreataxeCombo"/>) from over the shoulder (as
    /// the game's camera sees it), from the front and from the side; with -workshopCombo only the combo. Baked as the
    /// headsman's is: the body as a point cache, the axe and dummy rigid, sequence.json with the cameras and sound cues.
    /// </summary>
    public static class GreataxePreview
    {
        public const float Fps = 30f;
        private static readonly Vector3 DummyAt = new Vector3(0f, 0f, 2.8f);   // the combo carries the player 1.8 m on
        private static readonly string[] Moving = { "walk", "jog", "run", "crouch", "sneak", "block" };

        public static void Run()
        {
            int code = 1;
            try
            {
                Build(SlingerStage.Argument("-workshopOut"));
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("greataxe preview failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Build(string folder)
        {
            HeadsmanAxe.Stage();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject player = GreataxePlayer.Player();
            GameObject axe = GreataxeModel.Hang(player);
            GameObject dummy = HeadsmanProps.Dummy("dummy_front", DummyAt, 180f);
            Animator animator = GreataxePlayer.Animator(player);
            AnimatorController combo = GreataxeCombo.Controller($"{ReferenceAssets.Folder}/{GreataxePlayer.Subfolder}/greataxe.controller",
                GreataxeCombo.Clips(GreataxeCombo.Overhead));
            Prepare(animator, combo);
            var cache = new XbowCache(player.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            var rigid = new HeadsmanRigid();
            foreach (Renderer renderer in axe.GetComponentsInChildren<Renderer>())
                rigid.Add(renderer, "axe_held");
            foreach (Renderer renderer in dummy.GetComponentsInChildren<Renderer>())
                rigid.Add(renderer, renderer.name);
            var steps = new GreataxeSteps(player, animator, new GreataxeGrip(player, axe.transform.GetChild(0)), cache, rigid);
            Play(steps, combo, Environment.GetCommandLineArgs().Contains("-workshopCombo"));
            cache.Write(folder, Fps);
            rigid.Write(folder);
            steps.Write(folder);
            GreataxeFit.Report(steps.Palms);
            GreataxeFit.Axes(steps.Turns);
        }

        private static void Prepare(Animator animator, RuntimeAnimatorController controller)
        {
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // no camera sees it here
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
        }

        private static void Play(GreataxeSteps steps, AnimatorController combo, bool comboOnly)
        {
            if (!comboOnly)
            {
                steps.Stance("idle", 2.5f, "p_close");
                foreach (string state in Moving)
                    steps.Stance(state, 2f, "p_close");
                steps.Stance("idle", 0.8f, "p_close");
                steps.Jump("p_close");
            }
            foreach (string camera in new[] { "p_game", "p_front", "p_side" })
                steps.Combo(combo, "combo: slash, spin, overhead", camera);
            steps.Stance("idle", 1f, "p_game");
        }
    }
}
