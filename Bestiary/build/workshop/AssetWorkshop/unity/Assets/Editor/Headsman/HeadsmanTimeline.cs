using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.Headsman
{
    /// <summary>
    /// Steps the preview bake frame by frame: triggers each attack and plays until the controller is back in its carry
    /// idle, works out which attack plays and how far into its clip (the next state while blending in, else the
    /// current one), turns the creature by the attack's own turn (kept once the attack ends: the rear strike leaves it
    /// facing the other way), dresses the pose as the mod will (<see cref="HeadsmanPlayback"/>), runs the effects,
    /// marks the timeline and records the frame.
    /// </summary>
    public sealed class HeadsmanTimeline
    {
        [Serializable]
        private sealed class Sequence
        {
            public int[] frames, soundFrames, summonHit, summonForm, summonSolid;
            public string[] names, soundCues, summonBody;
            public float[] summonSpot, summonTargets;
        }

        /// <summary>The point-cache part name of each summon's body, set by the bake.</summary>
        public string[] SummonBodies = new string[0];

        private readonly List<(int frame, string cue)> sounds = new List<(int, string)>();

        private const float Dt = 1f / HeadsmanBake.Fps;
        private readonly GameObject boss;
        private readonly Animator animator;
        private readonly HeadsmanMove[] moves;
        private readonly HeadsmanPlayback playback;
        private readonly HeadsmanEffects effects;
        private readonly XbowCache cache;
        private readonly HeadsmanRigid rigid;
        private readonly List<(int frame, string camera)> cameras = new List<(int, string)>();
        private HeadsmanMove current;
        private float now, yaw, lastTime;
        private int frame;

        public HeadsmanTimeline(GameObject boss, Animator animator, HeadsmanMove[] moves, HeadsmanPlayback playback,
            HeadsmanEffects effects, XbowCache cache, HeadsmanRigid rigid)
        {
            (this.boss, this.animator, this.moves, this.playback) = (boss, animator, moves, playback);
            (this.effects, this.cache, this.rigid) = (effects, cache, rigid);
        }

        public void Idle(float seconds, string mark)
        {
            if (mark != null)
                Mark(mark, "close");
            for (int i = 0; i < Mathf.RoundToInt(seconds * HeadsmanBake.Fps); i++)
                Step();
        }

        public void Attack(HeadsmanMove move)
        {
            Mark(move.Title, move.Flight != Flight.None ? "wide" : move.Name == "rear" ? "rear" : "close");
            animator.SetTrigger(move.Name);
            int guard = Mathf.RoundToInt((move.Length + 2f) * HeadsmanBake.Fps);
            do
                Step();
            while (!Back() && --guard > 0);
            while (effects.Summon.Busy && --guard > -300)
                Step();   // a thrown axe's skeleton is still forming: let it finish before the gap
        }

        public void Loop(string trigger, string mark, int loops)
        {
            Mark(mark, "rear");
            animator.SetTrigger(trigger);
            string clip = trigger == HeadsmanAnimator.Walk ? HeadsmanAuthor.CarryClips[1].name : HeadsmanAuthor.CarryClips[2].name;
            float length = HeadsmanBuild.Clip(clip).length;
            for (int i = 0; i < Mathf.RoundToInt(loops * length * HeadsmanBake.Fps); i++)
                Step();
            animator.SetTrigger(HeadsmanAnimator.Stop);
        }

        public void Write(string folder)
        {
            var data = new Sequence
            {
                frames = cameras.Select(c => c.frame).ToArray(), names = cameras.Select(c => c.camera).ToArray(),
                soundFrames = sounds.Select(s => s.frame).ToArray(), soundCues = sounds.Select(s => s.cue).ToArray(),
            };
            List<HeadsmanSummon.Record> summons = effects.Summon.Records;
            data.summonHit = summons.Select(r => r.Hit).ToArray();
            data.summonForm = summons.Select(r => r.Form).ToArray();
            data.summonSolid = summons.Select(r => r.Solid).ToArray();
            data.summonBody = summons.Select(r => SummonBodies[r.Index]).ToArray();
            data.summonSpot = summons.SelectMany(r => new[] { r.Spot.x, r.Spot.y, r.Spot.z }).ToArray();
            data.summonTargets = summons.SelectMany(r => r.Targets.SelectMany(t => new[] { t.x, t.y, t.z })).ToArray();
            File.WriteAllText(Path.Combine(folder, "sequence.json"), JsonUtility.ToJson(data));
            Log.Info($"timeline: {frame} frames ({frame / HeadsmanBake.Fps:F1} s), {cameras.Count} camera cuts, {sounds.Count} sounds");
        }

        private void Mark(string name, string camera)
        {
            cache.Mark(name);
            cameras.Add((frame, camera));
        }

        private bool Back() =>
            !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName(HeadsmanAnimator.Movement);

        private void Step()
        {
            animator.Update(Dt);
            now += Dt;
            var (move, time) = Playing();
            if (current != null && move != current)
                yaw += current.Keys.RootYaw(current.Length);
            boss.transform.rotation = Quaternion.AngleAxis(yaw + (move == null ? 0f : move.Keys.RootYaw(time)), Vector3.up);
            playback.Dress(move, time);
            if (move != null)
                Moments(move, move == current ? lastTime : -1f, time);
            effects.Update(move, time, now, Dt);
            foreach (string cue in effects.Cues)
                sounds.Add((frame, cue));
            effects.Cues.Clear();
            (current, lastTime) = (move, time);
            cache.Capture(-1f);
            rigid.Capture();
            frame++;
        }

        private void Moments(HeadsmanMove move, float from, float time)
        {
            foreach (var (at, name) in move.Timeline())
                if (from < at && time >= at)
                    cache.Mark(name);
            foreach (var (at, cue) in move.Sounds)
                if (from < at && time >= at)
                    sounds.Add((frame, cue));
        }

        /// <summary>The attack playing and the seconds into its clip: the one blending in, else the current one.</summary>
        private (HeadsmanMove, float) Playing()
        {
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                HeadsmanMove incoming = moves.FirstOrDefault(m => next.IsName(m.Clip));
                if (incoming != null)
                    return (incoming, next.normalizedTime * incoming.Length);
            }
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            HeadsmanMove playing = moves.FirstOrDefault(m => info.IsName(m.Clip));
            return playing == null ? (null, -1f) : (playing, Mathf.Min(info.normalizedTime, 1f) * playing.Length);
        }
    }
}
