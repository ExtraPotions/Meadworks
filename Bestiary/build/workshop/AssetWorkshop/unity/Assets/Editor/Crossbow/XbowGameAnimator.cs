using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// The part of the game's Skeleton controller a bow shot runs through, rebuilt for the preview video with the
    /// crossbowman's clips in it, so the video shows the game's own blends: from Movement (here the crossbowman's own
    /// idle) any state goes to bow_idle on the attack_bow trigger in 0.25 s; bow_idle hands to attack_bow at 24% of its
    /// clip in 0.25 s; attack_bow (1.2x speed) hands back to Movement at 87% in 0.25 s. Transition times are fixed, as in
    /// the game.
    /// </summary>
    public static class XbowGameAnimator
    {
        public const string Trigger = "attack_bow";

        public static AnimatorController Build(AnimationClip aim, AnimationClip fire)
        {
            string path = ReferenceAssets.Folder + "/" + XbowReference.Subfolder + "/xbow_game_preview.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(Trigger, AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState movement = machine.AddState("Movement");
            movement.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(XbowBuild.ClipPath("ecp_xbow_idle"));
            machine.defaultState = movement;
            AnimatorState bowIdle = machine.AddState("bow_idle");
            bowIdle.motion = aim;
            AnimatorState attack = machine.AddState("attack_bow");
            attack.motion = fire;
            attack.speed = XbowClips.FireSpeed;
            AnimatorStateTransition start = machine.AddAnyStateTransition(bowIdle);
            start.AddCondition(AnimatorConditionMode.If, 0f, Trigger);
            Timed(start, false, 0f);
            Timed(bowIdle.AddTransition(attack), true, XbowClips.AimExit);
            Timed(attack.AddTransition(movement), true, XbowClips.FireExit);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void Timed(AnimatorStateTransition transition, bool exit, float exitTime)
        {
            transition.hasExitTime = exit;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = 0.25f;
            transition.canTransitionToSelf = false;
        }
    }
}
