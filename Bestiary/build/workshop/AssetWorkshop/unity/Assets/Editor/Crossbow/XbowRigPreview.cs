using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// The string and bolts for the preview, moved as the mod moves them (EliteCreaturesPack Crossbow/XbowRig), by
    /// seconds into the fire clip (below zero: not in it, so spanned and loaded): the string's middle sits in the nut,
    /// flies to its rest at the shot and shivers there, rides the left pinch from the hook to the nut, and stays in the
    /// nut; each half runs from its prod tip to the middle. A bolt lies in the groove until the shot and again from the
    /// lay; one is in the fingers from the grab in the quiver to the lay, and the quiver's first bolt is gone meanwhile.
    /// </summary>
    public sealed class XbowRigPreview
    {
        public const float Hook = 0.1f;   // seconds the string takes to come from its rest onto the hooking fingers

        private readonly Transform bow, tipA, tipB, rest, nut, stringA, stringB, pinch;
        private readonly GameObject grooveBolt, handBolt, quiverBolt;

        public XbowRigPreview(GameObject skeleton)
        {
            Transform Find(string name) => XbowReference.Bone(skeleton, name);
            (bow, tipA, tipB, rest, nut) = (Find("ecp_xbow_crossbow"), Find("ecp_xbow_tip_a"), Find("ecp_xbow_tip_b"), Find("ecp_xbow_rest"), Find("ecp_xbow_nut"));
            (stringA, stringB, pinch) = (Find("ecp_xbow_string_a"), Find("ecp_xbow_string_b"), Find("ecp_xbow_pinch"));
            grooveBolt = Find("ecp_xbow_bolt_groove").gameObject;
            handBolt = Find("ecp_xbow_bolt_hand").gameObject;
            quiverBolt = Find("ecp_xbow_bolt_quiver_0").gameObject;
        }

        public void Update(float time)
        {
            bool fired = time >= XbowClips.Fire;
            grooveBolt.SetActive(!fired || time >= XbowClips.Lay);
            handBolt.SetActive(time >= XbowClips.BoltGrab && time < XbowClips.Lay);
            quiverBolt.SetActive(!(time >= XbowClips.BoltGrab && time < XbowClips.Done));
            Vector3 middle = Middle(time);
            Stretch(stringA, tipA.localPosition, middle);
            Stretch(stringB, tipB.localPosition, middle);
        }

        /// <summary>The string's middle in the crossbow's frame.</summary>
        private Vector3 Middle(float time)
        {
            if (time < XbowClips.Fire || time >= XbowClips.Spanned)
                return nut.localPosition;
            if (time >= XbowClips.StringGrab)
                return Vector3.Lerp(rest.localPosition, bow.InverseTransformPoint(pinch.position), Mathf.SmoothStep(0f, 1f, (time - XbowClips.StringGrab) / Hook));
            float since = time - XbowClips.Fire;
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
