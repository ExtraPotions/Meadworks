using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// A controller per weapon, rebuilt from the game's own states: Movement (the Skeleton's idle) by default; on the
    /// trigger <see cref="Trigger"/> any state goes into the routine's first state, each state hands to the next at its
    /// exit time, and the last one back to Movement, all with the game's speeds and blends. Controllers and clips live in
    /// Assets/Reference/SkelArsenal (reference only, never in a bundle).
    /// </summary>
    public static class ArsenalAnimator
    {
        public const string Trigger = "attack";
        public const string Movement = "Movement";
        private const string Subfolder = "SkelArsenal";

        public static AnimatorController Build(Routine routine, AnimationClip idle)
        {
            string path = $"{ReferenceAssets.Folder}/{Subfolder}/{routine.Weapon}.controller";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(Trigger, AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState movement = machine.AddState(Movement);
            movement.motion = idle;
            machine.defaultState = movement;
            AnimatorState previous = null;
            for (int i = 0; i < routine.Steps.Length; i++)
                previous = Chain(machine, routine, i, previous);
            Out(previous.AddTransition(movement), routine.Steps[routine.Steps.Length - 1]);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>State i of the routine, entered from any state on the trigger (the first) or from the one before.</summary>
        private static AnimatorState Chain(AnimatorStateMachine machine, Routine routine, int i, AnimatorState previous)
        {
            Step step = routine.Steps[i];
            AnimatorState state = machine.AddState(StateName(routine, i));
            state.motion = Clip(step.Clip);
            state.speed = step.Speed;
            if (previous == null)
            {
                AnimatorStateTransition start = machine.AddAnyStateTransition(state);
                start.AddCondition(AnimatorConditionMode.If, 0f, Trigger);
                start.hasExitTime = false;
                start.hasFixedDuration = true;
                start.duration = routine.Enter;
                start.canTransitionToSelf = false;
            }
            else
                Out(previous.AddTransition(state), routine.Steps[i - 1]);
            return state;
        }

        private static void Out(AnimatorStateTransition transition, Step step)
        {
            transition.hasExitTime = true;
            transition.exitTime = step.Exit;
            transition.hasFixedDuration = step.FixedBlend;
            transition.duration = step.Blend;
        }

        /// <summary>The routine's i-th state: the game's name for it where the code looks for it (the bow's two states).</summary>
        public static string StateName(Routine routine, int i) =>
            routine.LeftHand ? (i == 0 ? "bow_idle" : "attack_bow") : $"{routine.Label.ToLowerInvariant()}_{i}";

        /// <summary>
        /// A game clip: the copy already in the project when another workshop tool brought it in (found by the game's GUID,
        /// so no second copy fights it for the GUID), otherwise imported into Assets/Reference/SkelArsenal.
        /// </summary>
        public static AnimationClip Clip(string referencePath)
        {
            string guid = GameGuid(referencePath + ".anim");
            string existing = AssetDatabase.GUIDToAssetPath(guid);
            string path = !string.IsNullOrEmpty(existing) && File.Exists(existing)
                ? existing
                : ReferenceAssets.Import(referencePath + ".anim", Subfolder);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
                throw new InvalidOperationException("no clip at " + path);
            return clip;
        }

        private static string GameGuid(string referencePath)
        {
            string meta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ValheimReference",
                "ExportedProject", "Assets", referencePath + ".meta");
            Match match = Regex.Match(File.ReadAllText(meta), @"guid: (\w+)");
            return match.Success ? match.Groups[1].Value : "";
        }
    }
}
