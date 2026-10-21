using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Headsman;

namespace Workshop.Greataxe
{
    /// <summary>
    /// Scores the game's player attack clips as the greataxe's spin:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.Greataxe.GreataxeProbe.Run
    /// Each clip is played on the player holding the greataxe, root still (the game moves a player by an attack's root
    /// motion but never turns it), and logged: how far the chest turns (a spin must turn it in the pose itself), how
    /// far apart the fists are, and in how many frames the left fist cannot be put on the haft (<see cref="GreataxeGrip"/>).
    /// </summary>
    public static class GreataxeProbe
    {
        private static readonly string[] Candidates =
        {
            "Characters/Player/model/old_PlayerCharacter/Atgeir360Attack.anim",
            "Characters/Player/model/battleaxe_anim/BattleAxe1.anim", "Characters/Player/model/battleaxe_anim/BattleAxe2.anim",
            "Characters/Player/model/old_PlayerCharacter/BattleAxeAltAttack.anim",
            "Characters/Player/model/battleaxe_anim/Standing Melee Combo Attack Ver. 1.anim",
            "3rd party/RPG Character Animation Pack/Animations/2Hand-Axe/2Hand-Axe-Attack1.anim",
            "Characters/Player/model/Greatsword_anim/Greatsword BaseAttack.anim", "Characters/Player/model/Greatsword_anim/Greatsword BaseAttack (1).anim",
            "Characters/Player/model/Greatsword_anim/Greatsword BaseAttack (2).anim", "Characters/Player/model/Greatsword_anim/Greatsword BaseAttack (3).anim",
            "Characters/Player/model/Greatsword_anim/Greatsword Secondary Attack.anim",
            "Characters/Player/model/DualAxes_anim/DualAxes Attack 4.anim", "Characters/Player/model/DualAxes_anim/DualAxes Attack Cleave.anim",
            "Characters/Player/model/axe_anim/Axe Secondary Attack.anim", "3rd party/RPG Character Animation Pack/Animations/2Hand-Spear/2Hand-Spear-Attack9.anim",
        };

        public static void Run()
        {
            int code = 1;
            try
            {
                Probe();
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("greataxe probe failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Probe()
        {
            HeadsmanAxe.Stage();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject player = GreataxePlayer.Player();
            GameObject axe = GreataxeModel.Hang(player);
            var grip = new GreataxeGrip(player, axe.transform.GetChild(0));
            Animator animator = GreataxePlayer.Animator(player);
            (animator.applyRootMotion, animator.cullingMode) = (false, AnimatorCullingMode.AlwaysAnimate);
            foreach (string path in Candidates)
                Score(player, animator, grip, GreataxePlayer.Clip(path));
        }

        private static void Score(GameObject player, Animator animator, GreataxeGrip grip, AnimationClip clip)
        {
            string asset = $"{ReferenceAssets.Folder}/{GreataxePlayer.Subfolder}/greataxe_probe.controller";
            AssetDatabase.DeleteAsset(asset);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(asset);
            controller.layers[0].stateMachine.AddState("clip").motion = clip;
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            Transform chest = GreataxePlayer.Bone(player, "Spine2"), right = GreataxePlayer.Bone(player, "RightHand_Attach"), left = GreataxePlayer.Bone(player, "LeftHand_Attach");
            (float turned, float last, float apart, int off, int frames) = (0f, float.NaN, 0f, 0, Mathf.CeilToInt(clip.length * 30f));
            for (int f = 0; f <= frames; f++)
            {
                animator.Play("clip", 0, f / (float)frames);
                animator.Update(0f);
                float yaw = Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(chest.forward, Vector3.up), Vector3.up);
                turned += float.IsNaN(last) ? 0f : Mathf.DeltaAngle(last, yaw);
                last = yaw;
                apart += (left.position - right.position).magnitude;
                grip.Apply();
                off += grip.Missed > 0.03f ? 1 : 0;
            }
            Log.Info($"greataxe probe {clip.name}: {clip.length:F2} s, chest turns {turned:F0} deg, fists {apart / (frames + 1):F2} m apart, left fist off the haft in {off} of {frames + 1} frames");
        }
    }
}
