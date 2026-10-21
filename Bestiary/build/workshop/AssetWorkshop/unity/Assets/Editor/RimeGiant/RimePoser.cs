using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// Poses the Troll through its own Animator, the way the game plays its clips: AnimationClip.SampleAnimation writes
    /// a humanoid clip's raw root rotation (the idle comes out facing backwards), while the Animator applies each clip's
    /// own settings (bake into pose, based upon body orientation). A throwaway controller in Assets/Reference/Troll holds
    /// one state per clip; a pose is Play at the clip time and a zero-length Update.
    /// </summary>
    public sealed class RimePoser
    {
        public static readonly string[] States = { "Idle", "Walk", "Run", "Punch", "Slam", "Throw", "Sleeping", "Wakeup" };
        private readonly Animator animator;
        private readonly Dictionary<string, float> lengths = new Dictionary<string, float>();

        public RimePoser(GameObject troll)
        {
            animator = troll.GetComponent<Animator>();
            animator.runtimeAnimatorController = Controller();
            animator.applyRootMotion = false;   // as the game's Troll
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
        }

        public float Length(string state) => lengths[state];

        /// <summary>The pose `seconds` into the state's clip (looping clips wrap).</summary>
        public void Pose(string state, float seconds)
        {
            animator.Play(state, 0, seconds / lengths[state]);
            animator.Update(0f);
        }

        private AnimatorController Controller()
        {
            string path = ReferenceAssets.Folder + "/" + RimeReference.Subfolder + "/rime_poser.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (string state in States)
            {
                AnimationClip clip = RimeReference.Clip(state);
                machine.AddState(state).motion = clip;
                lengths[state] = clip.length;
            }
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
