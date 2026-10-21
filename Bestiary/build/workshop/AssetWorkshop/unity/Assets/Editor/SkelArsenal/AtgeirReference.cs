using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Greataxe;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// Reference numbers for the Bone Atgeir's own player attacks: how the game's three atgeir attacks move the weapon and
    /// turn the body, read off the game's player controller at a few moments of each (never shipped; the clips are keyed
    /// from these numbers, AtgeirAuthor):
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.SkelArsenal.AtgeirReference.Run
    /// Logs, in the player's own frame (feet at the origin, facing +Z): the right fist's place, the haft's direction up
    /// towards the blade, the hips' and chest's yaw, and the hips' height.
    /// </summary>
    public static class AtgeirReference
    {
        private const string Controller = "Characters/Player/animation/Player_animator.controller";
        public static readonly Vector3 Up = new Vector3(0.339f, -0.143f, 0.930f).normalized;   // up the haft in the attach frame

        public static void Run()
        {
            int code = 1;
            try
            {
                Sample();
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("atgeir reference failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Sample()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ReferenceController.Import(Controller, "PlayerController"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject player = GreataxePlayer.Player();
            Animator animator = GreataxePlayer.Animator(player);
            (animator.runtimeAnimatorController, animator.cullingMode, animator.applyRootMotion) = (controller, AnimatorCullingMode.AlwaysAnimate, false);
            animator.Rebind();
            animator.SetInteger("statei", 7);
            animator.SetFloat("statef", 7);
            animator.SetBool("onGround", true);
            for (int i = 0; i < 90; i++)
                animator.Update(1f / 30f);
            Line(player, "stance", 0f);
            foreach (string trigger in new[] { "atgeir_attack0", "atgeir_attack1", "atgeir_attack2" })
                Attack(player, animator, trigger);
        }

        /// <summary>Straight into the attack's state (no blend), then a line every 0.1 s of its clip.</summary>
        private static void Attack(GameObject player, Animator animator, string trigger)
        {
            animator.Play("atgeir_attack " + trigger.Last(), 0, 0f);
            animator.Update(0f);
            float length = animator.GetCurrentAnimatorClipInfo(0)[0].clip.length;
            for (float t = 0f; t <= length + 0.001f; t += 0.05f)
            {
                animator.Play("atgeir_attack " + trigger.Last(), 0, t / length);
                animator.Update(0f);
                Line(player, trigger, t);
            }
        }

        private static void Line(GameObject player, string part, float t)
        {
            Transform fist = GreataxePlayer.Bone(player, "RightHand_Attach"), hips = GreataxePlayer.Bone(player, "Hips");
            Transform chest = GreataxePlayer.Bone(player, "Spine2"), left = GreataxePlayer.Bone(player, "LeftHand_Attach");
            Vector3 up = fist.TransformDirection(Up), at = fist.position, l = left.position;
            float Yaw(Transform b) => Mathf.Atan2(b.forward.x, b.forward.z) * Mathf.Rad2Deg;
            Log.Info($"atgeir ref {part} {t:0.00}: fist {at:F2} haft {up:F2} left {l:F2} hips yaw {Yaw(hips):F0} y {hips.position.y:F2} chest yaw {Yaw(chest):F0}");
        }
    }
}
