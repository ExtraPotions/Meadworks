using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// The pouch and bands for the preview, as the mod will move them (EliteCreaturesPack SlingRig): the pouch rests
    /// behind the fork, rides the drawing fingers from the load to the release, then snaps forward past the fork and
    /// settles; each band runs from its prong tip to its end of the pouch; a stone sits in the right hand from the
    /// reach into the satchel to the load, then in the pouch until the release.
    /// </summary>
    public sealed class SlingRigPreview
    {
        private const float PouchHalfWidth = 0.03f;
        private readonly Transform slingshot, pinch, pouch, stone, handStone, anchorA, anchorB, rest, bandA, bandB;
        private Vector3 released;

        public SlingRigPreview(GameObject greydwarf)
        {
            Transform Find(string name) => SlingerReference.Bone(greydwarf, name);
            slingshot = Find("ecr_sling_slingshot");
            pinch = Find("ecr_sling_pinch");
            pouch = Find("ecr_sling_pouch");
            stone = Find("ecr_sling_stone");
            handStone = Find("ecr_sling_hand_stone");
            anchorA = Find("ecr_sling_anchor_a");
            anchorB = Find("ecr_sling_anchor_b");
            rest = Find("ecr_sling_rest");
            bandA = Find("ecr_sling_band_a");
            bandB = Find("ecr_sling_band_b");
        }

        /// <summary>Seconds into the shot; below zero or past the recoil it is at rest.</summary>
        public void Update(float time)
        {
            bool drawn = time >= SlingClip.Load && time < SlingClip.Release;
            handStone.gameObject.SetActive(time >= SlingClip.Reach && time < SlingClip.Load);
            if (drawn)
            {
                pouch.position = pinch.position;
                released = slingshot.InverseTransformPoint(pinch.position);
            }
            else
            {
                pouch.localPosition = Recoil(time - SlingClip.Release);
            }
            Vector3 fork = (anchorA.localPosition + anchorB.localPosition) * 0.5f;
            pouch.localRotation = Quaternion.LookRotation(fork - pouch.localPosition, Vector3.up);
            stone.gameObject.SetActive(drawn);
            Stretch(bandA, anchorA.localPosition, pouch.localPosition + pouch.localRotation * Vector3.right * PouchHalfWidth);
            Stretch(bandB, anchorB.localPosition, pouch.localPosition - pouch.localRotation * Vector3.right * PouchHalfWidth);
        }

        /// <summary>After the release the pouch swings through the fork and back to rest, dying away in a quarter second.</summary>
        private Vector3 Recoil(float since)
        {
            if (since < 0f || since > 0.35f)
                return rest.localPosition;
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
