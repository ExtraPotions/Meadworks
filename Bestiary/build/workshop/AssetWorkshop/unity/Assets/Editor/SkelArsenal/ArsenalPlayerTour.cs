using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// Every state a player goes through with a bone weapon in hand, driven the way the game drives the player's animator,
    /// each marked on the timeline (the user: "show all possible player states: running, crouch, walk, fight, etc."):
    /// the stance; walking, jogging and running (forward_speed 2, 5 and 7.9, the movement blend's walk, jog and run);
    /// stopping; crouching, and sneaking crouched; blocking; a jump (take-off, in the air, landing); the fight (the
    /// primary combo, the bow drawn and loosed, the crossbow loaded then fired); the secondary attack where the weapon
    /// has one; the stance again. With a shield, after the block: a parry, walking behind it, a broken guard, a dodge.
    /// </summary>
    public static class ArsenalPlayerTour
    {
        public static void Play(PlayerRoutine routine, ArsenalPlayerSteps steps, Animator animator, XbowCache cache)
        {
            Part(cache, "stance", () => steps.Idle(1.2f));
            Part(cache, "walk", () => Move(steps, animator, 2f, 2f));
            Part(cache, "jog", () => Move(steps, animator, 5f, 2f));
            Part(cache, "run", () => Move(steps, animator, 7.9f, 2f));
            Part(cache, "stop", () => Move(steps, animator, 0f, 1f));
            animator.SetBool("crouching", true);
            Part(cache, "crouch", () => steps.Idle(1.5f));
            Part(cache, "sneak", () => Move(steps, animator, 2f, 2f));
            Move(steps, animator, 0f, 0.4f);
            animator.SetBool("crouching", false);
            Part(cache, "block", () => Held(steps, animator, "blocking", 1.6f));
            steps.Idle(0.6f);
            if (routine.Shield != null)
                Guard(steps, animator, cache);
            Part(cache, "jump", () => Jump(steps, animator));
            Part(cache, Fight(routine), () => Fight(routine, steps));
            if (routine.Secondary != null)
                Part(cache, "secondary", () => steps.Swing(routine.Secondary));
            Part(cache, "stance", () => steps.Idle(1.2f));
        }

        /// <summary>
        /// With a shield: a parry (the shield snapped up and down again: the game's parry is a block raised within a
        /// quarter second of the blow, with no clip of its own), walking behind the raised shield, the guard broken (the
        /// game's stagger, as when a block fails for want of stamina), and a dodge roll.
        /// </summary>
        private static void Guard(ArsenalPlayerSteps steps, Animator animator, XbowCache cache)
        {
            Part(cache, "parry", () => Held(steps, animator, "blocking", 0.4f));
            steps.Idle(0.8f);
            animator.SetBool("blocking", true);
            Part(cache, "block walking", () => Move(steps, animator, 2f, 2.4f));
            Move(steps, animator, 0f, 0.4f);
            Part(cache, "guard broken", () => Triggered(steps, animator, "stagger", 1.6f));
            animator.SetBool("blocking", false);
            steps.Idle(0.6f);
            Part(cache, "dodge", () => Triggered(steps, animator, "dodge", 1.4f));
        }

        private static void Triggered(ArsenalPlayerSteps steps, Animator animator, string trigger, float seconds)
        {
            animator.SetTrigger(trigger);
            steps.Idle(seconds);
        }

        private static void Part(XbowCache cache, string name, System.Action play)
        {
            cache.Mark(name);
            play();
        }

        private static string Fight(PlayerRoutine routine) =>
            routine.Bow != null ? "draw and loose" : routine.Crossbow ? "load, then fire" : "attack combo";

        private static void Fight(PlayerRoutine routine, ArsenalPlayerSteps steps)
        {
            if (routine.Bow != null)
                steps.Shoot();
            else if (routine.Crossbow)
                steps.LoadThenFire();
            foreach (string trigger in routine.Triggers)
                steps.Swing(trigger);
            steps.Idle(0.6f);
        }

        /// <summary>The movement blend eased to `speed` over a third of a second, then held for the rest of `seconds`.</summary>
        private static void Move(ArsenalPlayerSteps steps, Animator animator, float speed, float seconds)
        {
            float from = animator.GetFloat("forward_speed");
            int ease = Mathf.RoundToInt(0.33f * ArsenalPlayerSteps.Fps), all = Mathf.RoundToInt(seconds * ArsenalPlayerSteps.Fps);
            for (int i = 1; i <= all; i++)
            {
                animator.SetFloat("forward_speed", Mathf.Lerp(from, speed, Mathf.Clamp01(i / (float)ease)));
                steps.Idle(1f / ArsenalPlayerSteps.Fps);
            }
        }

        private static void Held(ArsenalPlayerSteps steps, Animator animator, string parameter, float seconds)
        {
            animator.SetBool(parameter, true);
            steps.Idle(seconds);
            animator.SetBool(parameter, false);
        }

        /// <summary>Take-off, half a second off the ground, the landing.</summary>
        private static void Jump(ArsenalPlayerSteps steps, Animator animator)
        {
            animator.SetTrigger("jump");
            animator.SetBool("onGround", false);
            steps.Idle(0.6f);
            animator.SetBool("onGround", true);
            steps.Idle(1f);
        }
    }
}
