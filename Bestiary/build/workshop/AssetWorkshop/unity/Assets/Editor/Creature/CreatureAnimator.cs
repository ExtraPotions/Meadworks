using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Builds the creature's animator controller against the contract the game's creature code drives:
    /// forward_speed, sideway_speed and turn_speed; the sleeping, dead, onGround (and similar) flags; the stagger
    /// trigger; and one trigger per attack, named after its clip. Attack states carry the tag "attack" and the stagger
    /// state "stagger", which is how the game tells an attack in progress.
    /// </summary>
    public static class CreatureAnimator
    {
        private static readonly string[] Floats = { "forward_speed", "sideway_speed", "turn_speed" };
        private static readonly string[] Bools =
            { "sleeping", "dead", "inWater", "onGround", "encumbered", "flying", "falling", "blocking" };
        private const float WalkSpeed = 0.66f;   // metres a second the hop clip covers at normal speed
        private const float RunSpeed = 5f;       // and the bound (run) clip
        private const float TopSpeed = 8f;       // the bound played faster, up to here

        public static AnimatorController Build(string folder, CreatureManifest info)
        {
            string path = folder + "/" + info.asset + ".controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var clips = LoadClips(folder + "/" + info.fbx);
            AddParameters(controller, info);
            var machine = controller.layers[0].stateMachine;
            var move = Locomotion(controller, clips["idle"], clips["walk"], clips.TryGetValue("run", out var run) ? run : null);
            var sleep = machine.AddState("sleep");
            sleep.motion = clips["sleep"];
            machine.defaultState = sleep;
            Link(sleep, move, AnimatorConditionMode.IfNot, "sleeping");
            Link(move, sleep, AnimatorConditionMode.If, "sleeping");
            foreach (var clip in info.clips.Where(c => c.tag == "attack" || c.tag == "stagger"))
                OneShot(machine, move, clips[clip.name], clip);
            Death(machine, clips["death"]);
            Log.Info($"animator {path}: {controller.parameters.Length} parameters, {machine.states.Length} states");
            return controller;
        }

        private static Dictionary<string, AnimationClip> LoadClips(string fbx) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);

        private static void AddParameters(AnimatorController controller, CreatureManifest info)
        {
            foreach (string name in Floats)
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (string name in Bools)
                controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            controller.AddParameter("stagger", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("jump", AnimatorControllerParameterType.Trigger);
            foreach (var clip in info.clips.Where(c => c.tag == "attack"))
                controller.AddParameter(clip.name, AnimatorControllerParameterType.Trigger);
            var parameters = controller.parameters;
            parameters.First(p => p.name == "sleeping").defaultBool = true;
            parameters.First(p => p.name == "onGround").defaultBool = true;
            controller.parameters = parameters;
        }

        /// <summary>
        /// Idle at rest, the hop at walking pace, blending into the run (bound) at its own pace and played faster above
        /// it; a creature without a run clip plays its hop faster instead.
        /// </summary>
        private static AnimatorState Locomotion(AnimatorController controller, AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            var state = controller.CreateBlendTreeInController("move", out BlendTree tree);
            tree.blendParameter = "forward_speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, WalkSpeed);
            if (run != null)
                tree.AddChild(run, RunSpeed);
            tree.AddChild(run != null ? run : walk, TopSpeed);
            var children = tree.children;
            children[children.Length - 1].timeScale = TopSpeed / (run != null ? RunSpeed : WalkSpeed);
            tree.children = children;
            return state;
        }

        /// <summary>An attack or stagger: entered from anywhere by its trigger, back to moving when it ends.</summary>
        private static void OneShot(AnimatorStateMachine machine, AnimatorState move, AnimationClip clip, ClipInfo info)
        {
            var state = machine.AddState(info.name);
            state.motion = clip;
            state.tag = info.tag;
            var enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, info.tag == "stagger" ? "stagger" : info.name);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            var leave = state.AddTransition(move);
            leave.hasExitTime = true;
            leave.exitTime = 0.95f;
            leave.duration = 0.1f;
        }

        private static void Death(AnimatorStateMachine machine, AnimationClip clip)
        {
            var state = machine.AddState("death");
            state.motion = clip;
            var enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "dead");
            enter.hasExitTime = false;
            enter.duration = 0.1f;
            enter.canTransitionToSelf = false;
        }

        private static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter)
        {
            var transition = from.AddTransition(to);
            transition.AddCondition(mode, 0f, parameter);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
        }
    }
}
