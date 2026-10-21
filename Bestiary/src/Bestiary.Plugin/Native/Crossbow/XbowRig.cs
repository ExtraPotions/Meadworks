// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.LocalEffects;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// Moves the crossbow's string and shows its bolts with the shot, on every peer that shows the crossbowman: the
    /// string's middle sits in the nut, flies to its rest at the shot and shivers there, comes onto the right fingers
    /// as they hook it and rides them back into the nut, where it clicks home; each half of the string runs from its
    /// prod tip to the middle. A bolt lies in the groove until the shot and again from the lay; one is in the fingers
    /// from the grab in the quiver to the lay, and the quiver's first bolt is gone meanwhile. Timed by the animator's own
    /// state (the "attack_bow" state playing the fire clip, which the game syncs), so it needs no network of its own and
    /// an interrupted reload is simply spanned and loaded again. The workshop's preview moves them the same way
    /// (AssetWorkshop Crossbow/XbowRigPreview). Only looks: the bolt leaves from the crossbow's muzzle, which this never
    /// moves, so a dedicated server (no graphics) runs none of it, and a crossbowman at rest is posed once, not every frame.
    /// </summary>
    public sealed class XbowRig : MonoBehaviour
    {
        // AssetWorkshop Crossbow/XbowClips: the fire clip's own seconds, and the share of it the state plays.
        private const float Fire = 0.32f, StringGrab = 1.52f, Spanned = 2.12f, BoltGrab = 2.76f, Lay = 3.60f, Done = 4.68f;
        private const float FireLength = Done / 0.8684211f;
        private const float Hook = 0.1f;   // seconds the string takes to come from its rest onto the hooking fingers
        private static readonly int ShotState = Animator.StringToHash("attack_bow");

        /// <summary>The string clicking into the nut; a local sound on each peer.</summary>
        public static GameObject? Click;

        private Animator? animator;
        private Transform bow = null!, tipA = null!, tipB = null!, rest = null!, nut = null!, stringA = null!, stringB = null!, pinch = null!;
        private GameObject grooveBolt = null!, handBolt = null!, quiverBolt = null!;
        private float last = -1f;
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
            Transform? b = Part("ecp_xbow_crossbow"), ta = Part("ecp_xbow_tip_a"), tb = Part("ecp_xbow_tip_b"), r = Part("ecp_xbow_rest"), n = Part("ecp_xbow_nut");
            Transform? sa = Part("ecp_xbow_string_a"), sb = Part("ecp_xbow_string_b"), p = Part("ecp_xbow_pinch");
            Transform? gb = Part(XbowKit.GrooveBolt), hb = Part(XbowKit.HandBolt), qb = Part(XbowKit.QuiverBolt + 0);
            if (animator == null || b == null || ta == null || tb == null || r == null || n == null || sa == null || sb == null || p == null
                || gb == null || hb == null || qb == null)
            {
                enabled = false;
                return;
            }
            (bow, tipA, tipB, rest, nut, stringA, stringB, pinch) = (b, ta, tb, r, n, sa, sb, p);
            (grooveBolt, handBolt, quiverBolt) = (gb.gameObject, hb.gameObject, qb.gameObject);
        }

        private void LateUpdate()
        {
            float time = FireTime();
            if (time < 0f && resting)
            {
                return;
            }
            resting = time < 0f;
            Pose(time);
        }

        /// <summary>The bolts and the string at `time` seconds into the fire clip (below zero: at rest), and the click.</summary>
        private void Pose(float time)
        {
            grooveBolt.SetActive(time < Fire || time >= Lay);
            handBolt.SetActive(time >= BoltGrab && time < Lay);
            quiverBolt.SetActive(time < BoltGrab || time >= Done);
            Vector3 middle = Middle(time);
            Stretch(stringA, tipA.localPosition, middle);
            Stretch(stringB, tipB.localPosition, middle);
            if (last < Spanned && time >= Spanned)
            {
                LocalEffect.Sound(Click, nut.position);
            }
            last = time;
        }

        /// <summary>Seconds into the fire clip, or below zero when the animator is not in (or heading into) the shot.</summary>
        private float FireTime()
        {
            AnimatorStateInfo state = animator!.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == ShotState ? Mathf.Clamp01(state.normalizedTime) * FireLength : -1f;
        }

        /// <summary>The string's middle in the crossbow's frame.</summary>
        private Vector3 Middle(float time)
        {
            if (time < Fire || time >= Spanned)
            {
                return nut.localPosition;
            }
            if (time >= StringGrab)
            {
                return Vector3.Lerp(rest.localPosition, bow.InverseTransformPoint(pinch.position), Mathf.SmoothStep(0f, 1f, (time - StringGrab) / Hook));
            }
            float since = time - Fire;
            return rest.localPosition + Vector3.forward * (0.03f * Mathf.Exp(-14f * since) * Mathf.Cos(70f * since));
        }

        private static void Stretch(Transform half, Vector3 from, Vector3 to)
        {
            Vector3 span = to - from;
            half.localPosition = from;
            half.localRotation = Quaternion.LookRotation(span.sqrMagnitude > 1e-6f ? span : Vector3.left, Vector3.up);
            half.localScale = new Vector3(1f, 1f, span.magnitude);
        }
    }
}
