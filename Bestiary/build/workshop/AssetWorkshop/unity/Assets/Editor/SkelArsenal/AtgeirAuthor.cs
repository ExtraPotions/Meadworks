using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop.Greataxe;
using Workshop.Slinger;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// The Bone Atgeir's own player attacks (Elite Creatures Pack plays them in place of the game's three atgeir attacks
    /// while a Bone Atgeir is in hand; the user: "make a custom animation for the bone atgeir ... make it perform like the
    /// original valheim atgeir"): the same moves as the game's thrust, second thrust and sweep, keyed every 0.1 s and at
    /// the hit from the player posed as the game's attack has it at that moment, but with the left fist put on the bone
    /// atgeir's haft (<see cref="GreataxeGrip"/>) in every key, and ending back in the atgeir stance so the blend out of
    /// the attack never swings the left hand off (the game's third swing ends far from its stance; the cross-fade back
    /// threw the left fist a metre from the haft). Each clip is the game's clip's length, so the game's events (trail,
    /// hit, speed), which the mod copies onto it, fall where they do:
    ///   Unity -batchmode -projectPath unity -executeMethod Workshop.SkelArsenal.AtgeirAuthor.Run
    /// Writes Assets/BundleExtras/ecp_skel_arsenal/ecp_atgeir_player_attack{0,1,2}.anim, which the arsenal bundle carries.
    /// </summary>
    public static class AtgeirAuthor
    {
        public const string Folder = "Assets/BundleExtras/ecp_skel_arsenal", Prefix = "ecp_atgeir_player_attack";
        private const string Controller = "Characters/Player/animation/Player_animator.controller";
        private const float Every = 0.1f, Settle = 0.12f;

        public static void Run()
        {
            int code = 1;
            try
            {
                Author();
                code = 0;
            }
            catch (Exception e)
            {
                Log.Error("atgeir author failed: " + e);
            }
            EditorApplication.Exit(code);
        }

        private static void Author()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ReferenceController.Import(Controller, "PlayerController"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject game = GreataxePlayer.Player(), ours = GreataxePlayer.Player();
            Animator animator = Stance(game, controller);
            var from = new HumanPoseHandler(animator.avatar, animator.transform);
            Animator oursAnimator = GreataxePlayer.Animator(ours);
            oursAnimator.enabled = false;
            var to = new HumanPoseHandler(oursAnimator.avatar, oursAnimator.transform);
            var stance = new HumanPose();
            from.GetHumanPose(ref stance);
            Directory.CreateDirectory(Folder);
            for (int i = 0; i < 3; i++)
                Attack(i, animator, from, to, ours, stance);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The game's player in its atgeir stance, settled past the controller's sit-and-rise intro.</summary>
        private static Animator Stance(GameObject player, RuntimeAnimatorController controller)
        {
            Animator animator = GreataxePlayer.Animator(player);
            (animator.runtimeAnimatorController, animator.cullingMode, animator.applyRootMotion) = (controller, AnimatorCullingMode.AlwaysAnimate, false);
            animator.Rebind();
            animator.SetInteger("statei", 7);
            animator.SetFloat("statef", 7);
            animator.SetBool("onGround", true);
            for (int f = 0; f < 90; f++)
                animator.Update(1f / 30f);
            return animator;
        }

        private static void Attack(int index, Animator animator, HumanPoseHandler from, HumanPoseHandler to, GameObject ours, HumanPose stance)
        {
            string state = "atgeir_attack " + index;
            animator.Play(state, 0, 0f);
            animator.Update(0f);
            AnimationClip gameClip = animator.GetCurrentAnimatorClipInfo(0)[0].clip;
            float length = gameClip.length, hit = Hit(gameClip);
            var times = Enumerable.Range(0, Mathf.FloorToInt(hit / Every) + 1).Select(k => k * Every)
                .Concat(new[] { hit, Mathf.Min(hit + Settle, length - 0.05f) }).Distinct().OrderBy(t => t).ToList();
            var track = new SlingTrack();
            var grip = new GreataxeGrip(ours, GreataxePlayer.Bone(ours, "RightHand_Attach"), h => AtgeirReference.Up * h, 0.15f, 1.3f);
            foreach (float t in times)
            {
                animator.Play(state, 0, t / length);
                animator.Update(0f);
                track.Add(t, Posed(from, to, grip));
            }
            track.Add(length, Posed(stance, to, grip));
            Save(track.ToClip(Prefix + index), gameClip, length, index, times.Count + 1);
        }

        /// <summary>The game player's pose now, on our player, the left fist put on the haft.</summary>
        private static HumanPose Posed(HumanPoseHandler from, HumanPoseHandler to, GreataxeGrip grip)
        {
            var pose = new HumanPose();
            from.GetHumanPose(ref pose);
            return Posed(pose, to, grip);
        }

        private static HumanPose Posed(HumanPose pose, HumanPoseHandler to, GreataxeGrip grip)
        {
            to.SetHumanPose(ref pose);
            grip.Apply();
            var result = new HumanPose();
            to.GetHumanPose(ref result);
            return result;
        }

        private static float Hit(AnimationClip clip) =>
            AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName == "OnAttackTrigger" || e.functionName == "Hit")
                .Select(e => e.time).DefaultIfEmpty(clip.length * 0.7f).First();

        private static void Save(AnimationClip clip, AnimationClip game, float length, int index, int keys)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(game);
            (settings.stopTime, settings.loopTime) = (length, false);
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            // The game clip's markers (trail, hit, speed: names, times and numbers only), so ours plays the attack as it does.
            AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(game).Where(e => e.objectReferenceParameter == null).ToArray());
            string path = $"{Folder}/{Prefix}{index}.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            Log.Info($"atgeir clip {clip.name}: {length:F3} s from the game's {game.name}, {keys} keys, hit at {Hit(game):F3}");
        }
    }
}
