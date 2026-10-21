// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// What the Executioner's clips cannot do, drawn on every peer after the animator by the clip's own time
    /// (<see cref="HeadsmanClock"/>): in the rear strike the skull snaps all the way round and the upper body wrings round
    /// at the waist to follow it (a skeleton turns further than its muscles go); the axe is gone from a throw's release
    /// and forms again in the raised hands (<see cref="HeadsmanForming"/>); chunks fly where the slam lands
    /// (<see cref="HeadsmanRocks"/>); and the sound cues play
    /// (<see cref="HeadsmanSounds"/>). Local only: the network carries the animator, and that is enough. A dedicated
    /// server only turns the body (the owner's cut follows the turned blade, <see cref="Edge"/>); the axe forming, the
    /// sounds and the rocks are for eyes and ears it does not have.
    /// </summary>
    public sealed class HeadsmanRig : MonoBehaviour
    {
        private Animator animator = null!;
        private Transform head = null!, neck = null!, spine = null!, chest = null!, axe = null!;
        private HeadsmanForming forming = null!;
        private HeadsmanMove? current;
        private float last = -1f;
        private bool draws;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            Transform? Bone(string name) => GameMaterials.Find(transform, name);
            Transform? h = Bone("Head"), n = Bone("Neck"), s = Bone("Spine"), c = Bone("Spine2"), a = Bone(HeadsmanKit.AxeName);
            if (animator == null || h == null || n == null || s == null || c == null || a == null)
            {
                enabled = false;
                return;
            }
            (head, neck, spine, chest, axe) = (h, n, s, c, a);
            forming = new HeadsmanForming(axe);
            draws = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        }

        /// <summary>The blade's edge in the world, where the axe bites the floor.</summary>
        public Vector3 Edge => axe.TransformPoint(HeadsmanAxe.Edge);

        private void LateUpdate()
        {
            var (move, time) = HeadsmanClock.Read(animator);
            if (move != current)
            {
                (current, last) = (move, -1f);
            }
            if (move == HeadsmanMoves.Rear)
            {
                Turn(time);
            }
            if (!draws)
            {
                last = time;
                return;
            }
            forming.Show(move, time);
            if (move != null)
            {
                Cues(move, time);
            }
            last = time;
        }

        /// <summary>The upper body turned about the lower back's upright line, then the skull about the chest's.</summary>
        private void Turn(float time)
        {
            float torso = HeadsmanMoves.TorsoSpin(time), skull = HeadsmanMoves.HeadSpin(time);
            if (Mathf.Abs(torso) > 0.01f)
            {
                spine.rotation = Quaternion.AngleAxis(torso, Vector3.up) * spine.rotation;
            }
            if (Mathf.Abs(skull) > 0.01f)
            {
                head.rotation = Quaternion.AngleAxis(skull, (neck.position - chest.position).normalized) * head.rotation;
            }
        }

        private void Cues(HeadsmanMove move, float time)
        {
            foreach (var (at, cue) in move.Sounds)
            {
                if (HeadsmanClock.Crossed(last, time, at))
                {
                    HeadsmanSounds.Play(cue, transform.position + Vector3.up);
                }
            }
            if (move == HeadsmanMoves.Slam && HeadsmanClock.Crossed(last, time, move.Hit))
            {
                HeadsmanRocks.Burst(HeadsmanGround.Floor(Edge));
            }
        }
    }
}
