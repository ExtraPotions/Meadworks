using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.Headsman
{
    /// <summary>
    /// A controller for the preview bake, blending the way the game's creature controllers do: the carry idle as
    /// "Movement", each attack a state entered from any state on a trigger named after it and left for Movement near its
    /// end, and the carry walk and run entered on "walk" and "run" and left on "stop". Fixed 0.2 s blends.
    /// </summary>
    public static class HeadsmanAnimator
    {
        public const float Blend = 0.2f;
        public const string Movement = "Movement", Walk = "walk", Run = "run", Stop = "stop";

        public static AnimatorController Build(HeadsmanMove[] moves)
        {
            string path = ReferenceAssets.Folder + "/" + XbowReference.Subfolder + "/headsman_preview.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState movement = machine.AddState(Movement);
            movement.motion = HeadsmanBuild.Clip(HeadsmanAuthor.CarryClips[0].name);
            machine.defaultState = movement;
            foreach (HeadsmanMove move in moves)
            {
                AnimatorState state = Enter(controller, machine, move.Name, move.Clip);
                Timed(state.AddTransition(movement), true, (move.Length - Blend) / move.Length);
            }
            controller.AddParameter(Stop, AnimatorControllerParameterType.Trigger);
            foreach (var (trigger, clip) in new[] { (Walk, HeadsmanAuthor.CarryClips[1].name), (Run, HeadsmanAuthor.CarryClips[2].name) })
            {
                AnimatorStateTransition leave = Enter(controller, machine, trigger, clip).AddTransition(movement);
                leave.AddCondition(AnimatorConditionMode.If, 0f, Stop);
                Timed(leave, false, 0f);
            }
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorState Enter(AnimatorController controller, AnimatorStateMachine machine, string trigger, string clip)
        {
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            AnimatorState state = machine.AddState(clip);
            state.motion = HeadsmanBuild.Clip(clip);
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            Timed(enter, false, 0f);
            return state;
        }

        private static void Timed(AnimatorStateTransition transition, bool exit, float exitTime)
        {
            transition.hasExitTime = exit;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = Blend;
            transition.canTransitionToSelf = false;
        }
    }
}
