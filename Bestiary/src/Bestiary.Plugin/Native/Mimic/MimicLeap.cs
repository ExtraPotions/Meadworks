// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// Carries the mimic forward during its lunge. The lunge animation plays the leap on the spot; this moves the
    /// creature along the same distance curve (AssetWorkshop clips.py LUNGE: nothing until frame 22, 1.5 m by 28, 2.45 m
    /// by the snap on 31, 2.55 m by 33, of 84), through the game's own root-motion hook, so the move runs on the owner and
    /// replicates like any creature's. Unity's root motion from the exported rig came out backwards or downwards, hence
    /// the curve here rather than in the clip.
    /// </summary>
    public class MimicLeap : MonoBehaviour
    {
        private static readonly int Lunge = Animator.StringToHash(MimicBite.Animation);
        private static readonly (float frame, float metres)[] Curve = { (22f, 0f), (28f, 1.5f), (31f, 2.45f), (33f, 2.55f) };
        private const float ClipFrames = 84f;
        private const float GameRootMotionScale = 55f;   // Character.ApplyRootMotion: velocity = accumulated motion * 55

        private Character _character = null!;
        private Animator _animator = null!;
        private ZNetView _nview = null!;
        private float _travelled;

        private void Awake()
        {
            _character = GetComponent<Character>();
            _animator = GetComponentInChildren<Animator>();
            _nview = GetComponent<ZNetView>();
        }

        private void FixedUpdate()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _animator == null)
            {
                return;
            }
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != Lunge)
            {
                _travelled = 0f;
                return;
            }
            float target = Distance(Mathf.Clamp01(state.normalizedTime) * ClipFrames) * transform.localScale.z;
            float step = target - _travelled;
            if (step > 0f)
            {
                // What the game turns into velocity for one physics step, so the creature covers exactly `step`.
                _character.AddRootMotion(transform.forward * (step / (GameRootMotionScale * Time.fixedDeltaTime)));
                _travelled = target;
            }
        }

        private static float Distance(float frame)
        {
            if (frame <= Curve[0].frame)
            {
                return 0f;
            }
            for (int i = 1; i < Curve.Length; i++)
            {
                if (frame <= Curve[i].frame)
                {
                    float t = (frame - Curve[i - 1].frame) / (Curve[i].frame - Curve[i - 1].frame);
                    return Mathf.Lerp(Curve[i - 1].metres, Curve[i].metres, t);
                }
            }
            return Curve[Curve.Length - 1].metres;
        }
    }
}
