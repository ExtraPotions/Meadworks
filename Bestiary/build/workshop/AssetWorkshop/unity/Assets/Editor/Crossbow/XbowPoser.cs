using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Poses the Skeleton through its own Animator, the way the game plays its clips (AnimationClip.SampleAnimation
    /// would write a humanoid clip's raw root rotation; the Animator applies each clip's own settings). A throwaway
    /// controller in Assets/Reference/Skeleton holds one state per clip; a pose is Play at the clip time and a
    /// zero-length Update. Extra clips (the crossbowman's own) can be added as states too. Several posers may share one
    /// skeleton: each puts its own controller back before it poses.
    /// </summary>
    public sealed class XbowPoser
    {
        public static readonly string[] GameStates = { "Idle", "Walk", "Run", "Aim", "Recoil" };
        private static int made;
        private readonly Animator animator;
        private readonly AnimatorController controller;
        private readonly Dictionary<string, float> lengths = new Dictionary<string, float>();

        public XbowPoser(GameObject skeleton, params AnimationClip[] extra)
        {
            animator = XbowReference.Animator(skeleton);
            controller = Controller(extra);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
        }

        public float Length(string state) => lengths[state];

        /// <summary>The pose `seconds` into the state's clip (looping clips wrap).</summary>
        public void Pose(string state, float seconds)
        {
            if (animator.runtimeAnimatorController != controller)
                animator.runtimeAnimatorController = controller;   // several posers share the one skeleton
            animator.Play(state, 0, seconds / lengths[state]);
            animator.Update(0f);
        }

        private AnimatorController Controller(AnimationClip[] extra)
        {
            string path = ReferenceAssets.Folder + "/" + XbowReference.Subfolder + $"/xbow_poser_{made++}.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            IEnumerable<(string, AnimationClip)> clips = GameStates.Select(s => (s, XbowReference.Clip(s)))
                .Concat(extra.Select(c => (c.name, c)));
            foreach (var (state, clip) in clips)
            {
                machine.AddState(state).motion = clip;
                lengths[state] = clip.length;
            }
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
