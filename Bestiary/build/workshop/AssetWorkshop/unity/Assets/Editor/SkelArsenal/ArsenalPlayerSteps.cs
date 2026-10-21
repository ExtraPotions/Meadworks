using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Greataxe;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// Steps one player through its weapon's routine at 30 frames a second, the way the game drives the player's animator:
    /// a trigger per combo step, each fired as the last attack ends; the bow drawn (bow_aim) and loosed (bow_fire); the
    /// crossbow fired, then reloaded (reload_crossbow held for the reload, then reload_crossbow_done). In an attack the
    /// clips' own Speed events set the animator's speed (the game's CharacterAnimEvent.Speed), out of one it is 1 again.
    /// Every frame is recorded (<see cref="XbowCache"/>).
    /// </summary>
    public sealed class ArsenalPlayerSteps
    {
        public const float Fps = 30f, Reload = 2.3f, Draw = 2.5f;   // the Bone Crossbow's reload; the bow's draw at no skill (BowFineWood)
        private const float Dt = 1f / Fps;

        private readonly Animator animator;
        private readonly XbowCache cache;
        private readonly ArsenalBow bow;
        private readonly Transform loaded, spent;
        private readonly float scale;
        private (int state, float time) last = (0, -1f);
        private bool aiming, loosing;

        /// <summary>Keeps the left fist on a two-handed haft after each pose (the atgeir), as the mod does; null for none.</summary>
        public GreataxeGrip Grip;

        /// <summary>The Bone Crossbow's string and bolts, as the mod moves them; null for none.</summary>
        public XbowPlayerRigPreview Rig;

        public int Frame { get; private set; }
        public int Attack { get; private set; } = -1;
        public int Loose { get; private set; } = -1;

        public ArsenalPlayerSteps(Animator animator, XbowCache cache, ArsenalBow bow, Transform loaded, Transform spent, float scale)
        {
            (this.animator, this.cache, this.bow, this.loaded, this.spent, this.scale) = (animator, cache, bow, loaded, spent, scale);
            Show(true);
        }

        public void Idle(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds * Fps); i++)
                Step();
        }

        /// <summary>One combo step: its trigger, then on until the attack is over.</summary>
        public void Swing(string trigger)
        {
            Attack = Attack < 0 ? Frame : Attack;
            animator.SetTrigger(trigger);
            int guard = 300;
            do
                Step();
            while (!InAttack() && --guard > 0);
            while (InAttack() && --guard > 0)
                Step();
        }

        /// <summary>The bow drawn a while, loosed, and back to the stance.</summary>
        public void Shoot()
        {
            Attack = Frame;
            animator.SetBool("bow_aim", true);
            aiming = true;
            for (int i = 0; i <= Mathf.RoundToInt(Draw * Fps); i++)
            {
                animator.SetFloat("drawpercent", i / (Draw * Fps));   // the game raises it as the draw goes on
                Step();
            }
            Idle(0.3f);
            animator.SetTrigger("bow_fire");   // loosed from the aim, as the game does: the aim is let go after
            (aiming, loosing) = (false, true);
            Idle(0.1f);
            animator.SetBool("bow_aim", false);
            animator.SetFloat("drawpercent", 0f);
            int guard = 300;
            while (InAttack() && --guard > 0)
                Step();
        }

        /// <summary>The crossbow loaded (the string drawn into the nut, a bolt laid), held ready, then fired from its stance.</summary>
        public void LoadThenFire()
        {
            Attack = Frame;
            if (Rig != null)
                Rig.Loaded = false;
            Show(false);
            animator.SetBool("reload_crossbow", true);
            Idle(Reload);
            if (Rig != null)
                Rig.Loaded = true;
            animator.SetTrigger("reload_crossbow_done");
            animator.SetBool("reload_crossbow", false);
            Show(true);
            Idle(1.2f);   // spanned and loaded, ready to fire
            animator.SetTrigger("crossbow_fire");
            Idle(0.1f);
            Rig?.Shot();   // the shot: the game clears its loaded flag as the attack fires
            Show(false);
            int guard = 300;
            while (InAttack() && --guard > 0)
                Step();
        }

        private void Show(bool full)
        {
            if (spent == null)
                return;
            loaded.localScale = Vector3.one * (full ? scale : 0f);
            spent.localScale = Vector3.one * (full ? 0f : scale);
        }

        private void Step()
        {
            animator.Update(Dt);
            Events();
            Grip?.Apply();
            Rig?.Update(animator);
            if (!InAttack())
                animator.speed = 1f;
            bow?.Update(aiming, loosing, Dt);
            loosing = false;
            if (bow != null && bow.JustLoosed)
                Loose = Frame;
            cache.Capture(-1f);
            Frame++;
        }

        /// <summary>The Speed events of the attack clip playing, passed this frame.</summary>
        private void Events()
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            if (!state.IsTag("attack") || clips.Length == 0)
                return;
            AnimationClip clip = clips[0].clip;
            float time = Mathf.Min(state.normalizedTime, 1f) * clip.length;
            float from = last.state == state.fullPathHash ? last.time : -1f;
            last = (state.fullPathHash, time);
            foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName == "Speed" && e.time > from && e.time <= time))
                animator.speed = e.floatParameter;
        }

        /// <summary>As the game's Humanoid.InAttack: blending into an attack, or in one.</summary>
        private bool InAttack() =>
            animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("attack") || animator.GetCurrentAnimatorStateInfo(0).IsTag("attack");
    }
}
