using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Headsman;

namespace Workshop.Greataxe
{
    /// <summary>
    /// Steps the player preview frame by frame the way the game plays a player: in an attack the clips' own Speed events
    /// set the animator's speed (the game's CharacterAnimEvent.Speed), out of one it is 1 again; root motion moves the
    /// player through the swings; the left fist is kept on the haft (<see cref="GreataxeGrip"/>, as the mod does); a
    /// sound cue at each swing's trail and at each hit. Marks the timeline,
    /// names the camera for each part and records every frame (<see cref="XbowCache"/>, <see cref="HeadsmanRigid"/>).
    /// </summary>
    public sealed class GreataxeSteps
    {
        [Serializable]
        private sealed class Sequence
        {
            public int[] frames, soundFrames, summonHit = new int[0], summonForm = new int[0], summonSolid = new int[0];
            public string[] names, soundCues, summonBody = new string[0];
            public float[] summonSpot = new float[0], summonTargets = new float[0];
        }

        /// <summary>
        /// The sounds: the Battleaxe's own swing at every step's trail (the game plays that one sound for all three of its
        /// swings, and for the wooden greatsword's whirl) and its hit at each hit (sfx.py PLAYER).
        /// </summary>
        public const string SwingCue = "g_swing", HitCue = "g_hit";

        private const float Dt = 1f / GreataxePreview.Fps;
        private readonly GameObject player;
        private readonly Animator animator;
        private readonly XbowCache cache;
        private readonly HeadsmanRigid rigid;
        private readonly List<(int frame, string camera)> cameras = new List<(int, string)>();
        private readonly List<(int frame, string cue)> sounds = new List<(int, string)>();
        private (int level, float time) last = (-1, 0f);
        private AnimationClip[] combo = new AnimationClip[0];
        private readonly Transform rightGrip, leftGrip;
        private readonly GreataxeGrip hold;
        private string part = "";
        private float lift;

        /// <summary>The left fist's grip (LeftHand_Attach) in the right's frame, each frame, by part of the preview.</summary>
        public readonly List<(string part, Vector3 palm)> Palms = new List<(string, Vector3)>();

        /// <summary>The left fist's attach turn in the right's frame, each frame, by part.</summary>
        public readonly List<(string part, Quaternion turn)> Turns = new List<(string, Quaternion)>();
        private int frame;

        public GreataxeSteps(GameObject player, Animator animator, GreataxeGrip hold, XbowCache cache, HeadsmanRigid rigid)
        {
            (this.player, this.animator, this.hold, this.cache, this.rigid) = (player, animator, hold, cache, rigid);
            (rightGrip, leftGrip) = (GreataxePlayer.Bone(player, "RightHand_Attach"), GreataxePlayer.Bone(player, "LeftHand_Attach"));
        }

        /// <summary>A stance or movement for `seconds`, blended into, in place.</summary>
        public void Stance(string state, float seconds, string camera)
        {
            Mark(state, camera);
            animator.applyRootMotion = false;
            animator.CrossFadeInFixedTime(state == "idle" ? GreataxeCombo.Movement : state, 0.25f);
            Steps(seconds);
        }

        /// <summary>A jump: take-off, the air and the landing, the body lifted along the jump's arc.</summary>
        public void Jump(string camera)
        {
            Mark("jump", camera);
            animator.CrossFadeInFixedTime("jump", 0.1f);
            for (int i = 0; i < 26; i++)
            {
                if (i == 16)
                    animator.CrossFadeInFixedTime("jump_loop", 0.1f);
                float u = Mathf.Clamp01((i - 4) / 20f);
                lift = 1.6f * u * (1f - u);
                Step();
            }
            lift = 0f;
            animator.CrossFadeInFixedTime("jump_end", 0.05f);
            Steps(0.2f);
            animator.CrossFadeInFixedTime(GreataxeCombo.Movement, 0.25f);
            Steps(0.8f);
        }

        /// <summary>The whole combo with `controller`, each swing starting as the last one's attack ends.</summary>
        public void Combo(RuntimeAnimatorController controller, string title, string camera)
        {
            animator.runtimeAnimatorController = controller;
            combo = new[] { "slash", "spin", "overhead" }.Select(part => controller.animationClips.First(c => c.name.StartsWith("greataxe_" + part))).ToArray();
            animator.Rebind();
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator.Update(0f);
            Mark(title, camera);
            Steps(0.8f);
            animator.applyRootMotion = true;
            for (int level = 0; level < 3; level++)
                Swing(level);
            animator.applyRootMotion = false;
            Steps(1.2f);
        }

        public void Steps(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds * GreataxePreview.Fps); i++)
                Step();
        }

        public void Write(string folder)
        {
            var data = new Sequence
            {
                frames = cameras.Select(c => c.frame).ToArray(), names = cameras.Select(c => c.camera).ToArray(),
                soundFrames = sounds.Select(s => s.frame).ToArray(), soundCues = sounds.Select(s => s.cue).ToArray(),
            };
            File.WriteAllText(Path.Combine(folder, "sequence.json"), JsonUtility.ToJson(data));
            Log.Info($"timeline: {frame} frames ({frame / GreataxePreview.Fps:F1} s), {cameras.Count} camera cuts, {sounds.Count} sounds");
        }

        private void Swing(int level)
        {
            string combo = part.Split('/')[0].Trim();
            part = $"{combo} / step {level}";
            animator.SetTrigger(GreataxeCombo.Trigger(level));
            int guard = 200;
            do
                Step();
            while (Attacking().level != level && --guard > 0);
            while (InAttack() && --guard > 0)
                Step();
            Log.Info($"greataxe swing {level} ends {player.transform.position.z:F2} m forward");
        }

        private void Mark(string name, string camera)
        {
            cache.Mark(name);
            cameras.Add((frame, camera));
            part = name;
        }

        private void Step()
        {
            animator.Update(Dt);
            player.transform.rotation = Quaternion.identity;   // the game moves a player by root motion, never turns it
            Events();
            if (!InAttack())
                animator.speed = 1f;
            Vector3 at = player.transform.position;
            player.transform.position = new Vector3(at.x, lift, at.z);
            hold.Apply();
            cache.Capture(-1f);
            rigid.Capture();
            Palms.Add((part, rightGrip.InverseTransformPoint(leftGrip.position)));
            Turns.Add((part, Quaternion.Inverse(rightGrip.rotation) * leftGrip.rotation));
            frame++;
        }

        /// <summary>The attack's clip events passed this frame: Speed sets the speed, the trail and hit are cued.</summary>
        private void Events()
        {
            var (level, time) = Attacking();
            float from = level == last.level ? last.time : -1f;
            last = (level, time);
            if (level < 0)
                return;
            foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(combo[level]).Where(e => e.time > from && e.time <= time))
            {
                if (e.functionName == "Speed")
                    animator.speed = e.floatParameter;
                else if (e.functionName == "TrailOn")
                    sounds.Add((frame, SwingCue));
                else if (GreataxeCombo.IsHit(e))
                    sounds.Add((frame, HitCue));
            }
        }

        /// <summary>The combo step playing and the seconds into its clip: the one blending in, else the current one.</summary>
        private (int level, float time) Attacking()
        {
            if (animator.IsInTransition(0))
            {
                int next = Level(animator.GetNextAnimatorStateInfo(0));
                if (next >= 0)
                    return (next, Seconds(animator.GetNextAnimatorStateInfo(0), next));
            }
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            int level = Level(current);
            return level < 0 ? (-1, 0f) : (level, Seconds(current, level));
        }

        /// <summary>Seconds into the combo step's clip.</summary>
        private float Seconds(AnimatorStateInfo state, int level) => Mathf.Min(state.normalizedTime, 1f) * combo[level].length;

        private static int Level(AnimatorStateInfo state) =>
            Enumerable.Range(0, 3).Where(l => state.IsName(GreataxeCombo.State(l))).DefaultIfEmpty(-1).First();

        /// <summary>As the game's Humanoid.InAttack: blending into an attack, or in one (also while blending out of it).</summary>
        private bool InAttack() =>
            animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("attack") || animator.GetCurrentAnimatorStateInfo(0).IsTag("attack");
    }
}
