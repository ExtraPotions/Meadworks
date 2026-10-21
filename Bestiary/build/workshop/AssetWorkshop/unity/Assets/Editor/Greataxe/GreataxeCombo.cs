using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Workshop.Greataxe
{
    /// <summary>
    /// How a player swings the Executioner's Greataxe: the game's own player clips put into the Battleaxe's three combo
    /// states (the mod does the same with an override while the greataxe is in hand): the Battleaxe's first swing, a
    /// slash in front; the greatsword's whirling second swing; then the Battleaxe's own third swing, an overhead (the
    /// user's pick over the sledge's smash), slowed after its hit so the player recovers for <see cref="Recovery"/> seconds. The states and
    /// their blends are the game's (Player_animator.controller): entered from any state on the trigger
    /// battleaxe_attack0..2 over 0.1, 0.3 and 0.3 s, left for the movement at 90, 80 and 100 % of the clip over 0.4, 0.3
    /// and 0.3 s. The stance and movement are the game's Battleaxe clips (the item is a Battleaxe-type weapon).
    /// </summary>
    public static class GreataxeCombo
    {
        public const float Recovery = 0.75f;
        public const string Movement = "Movement";
        private const string Battleaxe = "Characters/Player/model/battleaxe_anim/";
        private const string Old = "Characters/Player/model/old_PlayerCharacter/";

        public const string Slash = Battleaxe + "BattleAxe1.anim";
        /// <summary>
        /// The spin: the greatsword's second swing, a whirl three quarters round with both fists together on the grip
        /// (the atgeir's 360 spin holds a spear's shaft at arm's length, where the left fist cannot reach this haft:
        /// GreataxeProbe).
        /// </summary>
        public const string Spin = "Characters/Player/model/Greatsword_anim/Greatsword BaseAttack (2).anim";

        /// <summary>The overhead, the combo's third swing.</summary>
        public const string Overhead = "3rd party/RPG Character Animation Pack/Animations/2Hand-Axe/BattleAxe_Combo3.anim";

        /// <summary>The Battleaxe stance and movement: state name, clip.</summary>
        public static readonly (string state, string path)[] Stances =
        {
            ("idle", Battleaxe + "Idle_Battleaxe.anim"), ("walk", "Characters/Player/model/Movement_anim/Walk New Battleaxe.anim"),
            ("jog", "Characters/Player/model/Movement_anim/Jog New Battleaxe.anim"), ("run", "Characters/Player/model/Movement_anim/Run New Battleaxe.anim"),
            ("crouch", Battleaxe + "Battleaxe Crouch Idle.anim"), ("sneak", Battleaxe + "Battleaxe Sneak Walk.anim"),
            ("block", Old + "Block idle.anim"), ("jump", Battleaxe + "Battleaxe Jump.anim"),
            ("jump_loop", Battleaxe + "Battleaxe Jump Loop.anim"), ("jump_end", Battleaxe + "Battleaxe Jump End.anim"),
        };

        /// <summary>Each combo state's blend in, the share of its clip it leaves at, and its blend out.</summary>
        public static readonly (float blendIn, float exit, float blendOut)[] States = { (0.1f, 0.9f, 0.4f), (0.3f, 0.8f, 0.3f), (0.3f, 1f, 0.3f) };

        public static string Trigger(int level) => "battleaxe_attack" + level;

        public static string State(int level) => "battleaxe_attack " + level;

        public static bool IsHit(AnimationEvent e) => e.functionName == "OnAttackTrigger" || e.functionName == "Hit";

        /// <summary>The combo's three clips with `overhead` last, as the mod makes them.</summary>
        public static AnimationClip[] Clips(string overhead) => new[]
        {
            Copy(Slash, "slash", null), Copy(Spin, "spin", null), Copy(overhead, "overhead_" + System.IO.Path.GetFileNameWithoutExtension(overhead), Recovered),
        };

        /// <summary>
        /// The overhead's own events up to its hit, then one Speed event that slows the rest of the clip so the recovery
        /// (the rest of the clip and the blend out, while the game still counts the attack) lasts <see cref="Recovery"/>.
        /// </summary>
        public static AnimationEvent[] Recovered(AnimationClip clip)
        {
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            float hit = events.Where(IsHit).Select(e => e.time).DefaultIfEmpty(clip.length * 0.7f).First();
            float speed = (clip.length - hit) / (Recovery - States[2].blendOut);
            var slow = new AnimationEvent { functionName = "Speed", floatParameter = speed, time = Mathf.Min(hit + 0.01f, clip.length) };
            return events.Where(e => e.time <= hit || e.functionName != "Speed").Append(slow).OrderBy(e => e.time).ToArray();
        }

        /// <summary>A controller with the stance states and the three combo states, blending as the game's does.</summary>
        public static AnimatorController Controller(string path, AnimationClip[] combo)
        {
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState movement = machine.AddState(Movement);
            movement.motion = GreataxePlayer.Clip(Stances[0].path);
            machine.defaultState = movement;
            foreach (var (state, clip) in Stances.Skip(1))
                machine.AddState(state).motion = GreataxePlayer.Clip(clip);
            for (int level = 0; level < combo.Length; level++)
                AddAttack(controller, machine, movement, level, combo[level]);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void AddAttack(AnimatorController controller, AnimatorStateMachine machine, AnimatorState movement, int level, AnimationClip clip)
        {
            controller.AddParameter(Trigger(level), AnimatorControllerParameterType.Trigger);
            AnimatorState state = machine.AddState(State(level));
            state.motion = clip;
            state.tag = "attack";
            var (blendIn, exit, blendOut) = States[level];
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, Trigger(level));
            (enter.hasExitTime, enter.duration, enter.hasFixedDuration, enter.canTransitionToSelf) = (false, blendIn, true, true);
            AnimatorStateTransition leave = state.AddTransition(movement);
            (leave.hasExitTime, leave.exitTime, leave.duration, leave.hasFixedDuration) = (true, exit, blendOut, true);
        }

        /// <summary>The copies made this run, by name: the combos share their slash and spin.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, AnimationClip> Copies =
            new System.Collections.Generic.Dictionary<string, AnimationClip>();

        private static AnimationClip Copy(string path, string name, System.Func<AnimationClip, AnimationEvent[]> events)
        {
            if (Copies.TryGetValue(name, out AnimationClip made) && made != null)
                return made;
            AnimationClip game = GreataxePlayer.Clip(path);
            AnimationClip copy = Object.Instantiate(game);
            copy.name = "greataxe_" + name;
            if (events != null)
                AnimationUtility.SetAnimationEvents(copy, events(game));
            string asset = ReferenceAssets.Folder + "/" + GreataxePlayer.Subfolder + "/" + copy.name + ".anim";
            AssetDatabase.DeleteAsset(asset);
            AssetDatabase.CreateAsset(copy, asset);
            Copies[name] = copy;
            return copy;
        }
    }
}
