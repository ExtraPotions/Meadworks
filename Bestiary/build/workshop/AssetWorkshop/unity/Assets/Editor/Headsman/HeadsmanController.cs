using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// The Crypt Executioner's animator controller for the mod. It has every parameter the game's Skeleton controller
    /// has (the game's creature code sets them all: forward_speed, sideway_speed, turn_speed, footstep, inWater, dead,
    /// wakeup, and the triggers jump, attack, attack_bow, stagger, attack_mace, attack_fire) and one trigger per attack,
    /// named after its clip (the attack items' m_attackAnimation). "Movement" blends the carry idle, walk and run as the
    /// Skeleton's does (2D on sideway_speed and forward_speed: idle at 0,0, walk at 0,1, run at 0,3, walk at +-0.75,0);
    /// each attack is entered from any state on its trigger, tagged "attack" (how the game tells an attack in
    /// progress), and hands back to Movement as it ends; "Stagger" plays a placeholder the mod swaps for the Skeleton's
    /// own stagger clip at runtime (the bundle never carries the game's clips).
    /// </summary>
    public static class HeadsmanController
    {
        public const string Name = "ecp_headsman_animator", StaggerSlot = "ecp_headsman_stagger";
        private const float Blend = 0.2f;
        private static readonly string[] Floats = { "forward_speed", "sideway_speed", "turn_speed", "footstep" };
        private static readonly string[] Bools = { "inWater", "dead", "wakeup" };
        private static readonly string[] Triggers = { "jump", "attack", "attack_bow", "stagger", "attack_mace", "attack_fire" };

        public static string Build(HeadsmanMove[] moves)
        {
            string path = HeadsmanBuild.Folder + "/" + Name + ".controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            Parameters(controller, moves);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState movement = Movement(controller);
            machine.defaultState = movement;
            Stagger(machine, movement);
            foreach (HeadsmanMove move in moves)
                Attack(machine, movement, move);
            AssetDatabase.SaveAssets();
            Log.Info($"animator {path}: {controller.parameters.Length} parameters, {machine.states.Length} states");
            return path;
        }

        private static void Parameters(AnimatorController controller, HeadsmanMove[] moves)
        {
            foreach (string name in Floats)
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (string name in Bools)
                controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            foreach (string name in Triggers.Concat(moves.Select(m => m.Clip)))
                controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        private static AnimatorState Movement(AnimatorController controller)
        {
            AnimatorState state = controller.CreateBlendTreeInController("Movement", out BlendTree tree);
            state.tag = "idle";
            (tree.blendType, tree.blendParameter, tree.blendParameterY) = (BlendTreeType.FreeformDirectional2D, "sideway_speed", "forward_speed");
            AnimationClip idle = Clip(0), walk = Clip(1), run = Clip(2);
            tree.AddChild(idle, Vector2.zero);
            tree.AddChild(walk, new Vector2(0f, 1f));
            tree.AddChild(run, new Vector2(0f, 3f));
            tree.AddChild(walk, new Vector2(-0.75f, 0f));
            tree.AddChild(walk, new Vector2(0.75f, 0f));
            return state;
        }

        private static AnimationClip Clip(int carry) => HeadsmanBuild.Clip(HeadsmanAuthor.CarryClips[carry].name);

        /// <summary>The stagger, on the placeholder the mod replaces; entered as the Skeleton's is, played at its speed.</summary>
        private static void Stagger(AnimatorStateMachine machine, AnimatorState movement)
        {
            string path = HeadsmanBuild.Folder + "/" + StaggerSlot + ".anim";
            AssetDatabase.DeleteAsset(path);
            var slot = new AnimationClip { name = StaggerSlot };
            AssetDatabase.CreateAsset(slot, path);
            AnimatorState state = machine.AddState("Stagger");
            (state.motion, state.tag, state.speed) = (slot, "stagger", 0.7f);
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "stagger");
            (enter.hasExitTime, enter.duration, enter.hasFixedDuration, enter.canTransitionToSelf) = (false, 0.1f, true, false);
            AnimatorStateTransition leave = state.AddTransition(movement);
            (leave.hasExitTime, leave.exitTime, leave.duration, leave.hasFixedDuration) = (true, 0.9f, Blend, true);
        }

        private static void Attack(AnimatorStateMachine machine, AnimatorState movement, HeadsmanMove move)
        {
            AnimatorState state = machine.AddState(move.Clip);
            (state.motion, state.tag) = (HeadsmanBuild.Clip(move.Clip), "attack");
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, move.Clip);
            (enter.hasExitTime, enter.duration, enter.hasFixedDuration, enter.canTransitionToSelf) = (false, Blend, true, false);
            AnimatorStateTransition leave = state.AddTransition(movement);
            (leave.hasExitTime, leave.exitTime, leave.duration, leave.hasFixedDuration) = (true, (move.Length - Blend) / move.Length, Blend, true);
        }
    }
}
