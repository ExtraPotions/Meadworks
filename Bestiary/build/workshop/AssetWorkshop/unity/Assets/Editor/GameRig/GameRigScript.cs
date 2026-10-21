using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// What the preview and the playback check put the game's controller through, read from the controller itself so
    /// it fits any creature: waking (a `wakeup` bool, when it has one), idle, walk and run at the forward speeds its
    /// locomotion blend tree places its clips at, every attack trigger it declares, then stagger. Each phase is a set of
    /// parameters at its start and a length in seconds; `Still` is the moment (seconds in) a still is taken.
    /// </summary>
    public sealed class GameRigPhase
    {
        public string Label;
        public float Seconds;
        public float Still;
        public Action<Animator> Begin;
    }

    public static class GameRigScript
    {
        private static readonly HashSet<string> NotAttacks = new HashSet<string>
        {
            "stagger", "jump", "fly_takeoff", "fly_land", "consume", "attack_abort", "detach", "attach", "interact",
            "eat", "equip_hip", "footstep",
        };

        public static List<GameRigPhase> Phases(AnimatorController controller)
        {
            var phases = new List<GameRigPhase>();
            var parameters = controller.parameters.ToDictionary(p => p.name, p => p.type);
            if (parameters.TryGetValue("wakeup", out var wake) && wake == AnimatorControllerParameterType.Bool)
                phases.Add(Phase("wakeup", 3.0f, 1.2f, a => a.SetBool("wakeup", true)));
            phases.Add(Phase("idle", 2.0f, 1.0f, a => Speed(a, 0f)));
            foreach (var (label, speed) in Speeds(controller))
                phases.Add(Phase($"{label} {speed:0.#} m/s", 2.4f, 1.6f, a => Speed(a, speed)));
            phases.Add(Phase("idle", 0.8f, -1f, a => Speed(a, 0f)));
            foreach (string trigger in parameters.Where(p => p.Value == AnimatorControllerParameterType.Trigger)
                         .Select(p => p.Key).Where(n => !NotAttacks.Contains(n)))
                phases.Add(Phase(trigger, AttackSeconds(controller, trigger), Hit(controller, trigger), a => a.SetTrigger(trigger)));
            if (parameters.ContainsKey("stagger"))
                phases.Add(Phase("stagger", 2.2f, 0.45f, a => a.SetTrigger("stagger")));
            phases.Add(Phase("idle", 1.0f, -1f, a => Speed(a, 0f)));
            return phases;
        }

        private static GameRigPhase Phase(string label, float seconds, float still, Action<Animator> begin) =>
            new GameRigPhase { Label = label, Seconds = seconds, Still = still, Begin = begin };

        private static void Speed(Animator animator, float speed)
        {
            if (animator.parameters.Any(p => p.name == "wakeup"))
                animator.SetBool("wakeup", false);
            animator.SetFloat("forward_speed", speed);
        }

        /// <summary>("walk", m/s) and ("run", m/s): the slowest and fastest forward points of the locomotion blend tree.</summary>
        private static IEnumerable<(string, float)> Speeds(AnimatorController controller)
        {
            BlendTree tree = Locomotion(controller);
            float[] forward = tree == null ? new float[0] : tree.children.Select(c => Forward(tree, c)).Where(v => v > 0.1f)
                .Distinct().OrderBy(v => v).ToArray();
            if (forward.Length > 0)
                yield return ("walk", forward.First());
            if (forward.Length > 1)
                yield return ("run", forward.Last());
        }

        private static float Forward(BlendTree tree, ChildMotion child) =>
            tree.blendType == BlendTreeType.Simple1D ? child.threshold
                : tree.blendParameterY == "forward_speed" ? child.position.y : child.position.x;

        private static BlendTree Locomotion(AnimatorController controller) =>
            controller.layers[0].stateMachine.states.Select(s => s.state)
                .Where(s => s.tag == "idle" || s.name == "Movement").Select(s => s.motion).OfType<BlendTree>().FirstOrDefault();

        /// <summary>How long the attack the trigger starts plays (its clip over its speed), plus the blend back.</summary>
        private static float AttackSeconds(AnimatorController controller, string trigger)
        {
            AnimatorState state = Target(controller, trigger);
            var clip = state?.motion as AnimationClip;
            float seconds = clip == null ? 2f : clip.length / Mathf.Max(0.1f, Mathf.Abs(state.speed));
            return Mathf.Clamp(seconds + 0.6f, 1.5f, 6f);
        }

        /// <summary>The still's moment: 45 % into the attack's clip (the game's median first hit) plus the blend in.</summary>
        private static float Hit(AnimatorController controller, string trigger) => (AttackSeconds(controller, trigger) - 0.6f) * 0.45f + 0.25f;

        /// <summary>The state an Any State transition on the trigger leads to (the attack), or null.</summary>
        public static AnimatorState Target(AnimatorController controller, string trigger) =>
            controller.layers[0].stateMachine.anyStateTransitions
                .FirstOrDefault(t => t.conditions.Any(c => c.parameter == trigger))?.destinationState;
    }
}
