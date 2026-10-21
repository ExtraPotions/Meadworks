// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Bestiary.Runtime.Native.Slinger
{
    /// <summary>
    /// Moves the slingshot's pouch and bands with the shot, on every peer that shows the slinger: the pouch rests
    /// behind the fork, rides the drawing fingers from the grab to the release, then snaps forward through the fork and
    /// settles; each band runs from its prong tip to its end of the pouch; the pebble sits in the pouch while drawn.
    /// Timed by the animator's own state (the "throw" state playing the shot clip, which the game syncs), so it needs
    /// no network of its own and an interrupted shot simply lets go. The workshop's preview moves them the same way
    /// (AssetWorkshop Slinger/SlingRigPreview). Only looks: the stone leaves from the fork's muzzle, which this never
    /// moves, so a dedicated server (no graphics) runs none of it, and a slinger at rest is posed once, not every frame.
    /// </summary>
    public sealed class SlingRig : MonoBehaviour
    {
        private const float ClipLength = 2.0f;       // AssetWorkshop Slinger/SlingClip: Length, Grab, Release
        private const float Grab = 0.52f;
        private const float Release = 1.12f;
        private const float PouchHalfWidth = 0.03f;
        private static readonly int ShotState = Animator.StringToHash("throw");

        private Animator? animator;
        private Transform slingshot = null!, pinch = null!, pouch = null!, pebble = null!;
        private Transform anchorA = null!, anchorB = null!, rest = null!, bandA = null!, bandB = null!;
        private Vector3 released;
        private bool resting;

        private void Awake()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                enabled = false;
                return;
            }
            animator = GetComponentInChildren<Animator>();
            Transform? Part(string name) => GameMaterials.Find(transform, name);
            Transform? s = Part("ecr_sling_slingshot"), p = Part("ecr_sling_pinch"), q = Part("ecr_sling_pouch"), st = Part("ecr_sling_stone");
            Transform? a = Part("ecr_sling_anchor_a"), b = Part("ecr_sling_anchor_b"), r = Part("ecr_sling_rest");
            Transform? ba = Part("ecr_sling_band_a"), bb = Part("ecr_sling_band_b");
            if (animator == null || s == null || p == null || q == null || st == null || a == null || b == null || r == null || ba == null || bb == null)
            {
                enabled = false;
                return;
            }
            (slingshot, pinch, pouch, pebble, anchorA, anchorB, rest, bandA, bandB) = (s, p, q, st, a, b, r, ba, bb);
        }

        private void LateUpdate()
        {
            float time = ShotTime();
            if (time < 0f && resting)
            {
                return;
            }
            resting = time < 0f;
            Pose(time);
        }

        /// <summary>The pouch, pebble and bands at `time` seconds into the shot (below zero: at rest).</summary>
        private void Pose(float time)
        {
            bool drawn = time >= Grab && time < Release;
            if (drawn)
            {
                pouch.position = pinch.position;
                released = slingshot.InverseTransformPoint(pinch.position);
            }
            else
            {
                pouch.localPosition = Recoil(time < 0f ? -1f : time - Release);
            }
            Vector3 fork = (anchorA.localPosition + anchorB.localPosition) * 0.5f;
            pouch.localRotation = Quaternion.LookRotation(fork - pouch.localPosition, Vector3.up);
            pebble.gameObject.SetActive(drawn);
            Stretch(bandA, anchorA.localPosition, pouch.localPosition + pouch.localRotation * Vector3.right * PouchHalfWidth);
            Stretch(bandB, anchorB.localPosition, pouch.localPosition - pouch.localRotation * Vector3.right * PouchHalfWidth);
        }

        /// <summary>Seconds into the shot clip, or below zero when the animator is not in (or heading into) the shot.</summary>
        private float ShotTime()
        {
            AnimatorStateInfo state = animator!.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == ShotState ? Mathf.Clamp01(state.normalizedTime) * ClipLength : -1f;
        }

        /// <summary>After the release the pouch swings through the fork and back to rest, dying away in a third of a second.</summary>
        private Vector3 Recoil(float since)
        {
            if (since < 0f || since > 0.35f)
            {
                return rest.localPosition;
            }
            float swing = Mathf.Exp(-10f * since) * Mathf.Cos(22f * since);
            return rest.localPosition + (released - rest.localPosition) * swing;
        }

        private static void Stretch(Transform band, Vector3 from, Vector3 to)
        {
            Vector3 span = to - from;
            band.localPosition = from;
            band.localRotation = Quaternion.LookRotation(span.sqrMagnitude > 1e-6f ? span : Vector3.back, Vector3.up);
            band.localScale = new Vector3(1f, 1f, span.magnitude);
        }
    }
}
