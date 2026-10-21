using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Workshop.Crossbow;
using Workshop.Greataxe;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// The Bone Crossbow's string and bolts in the player preview, moved as the mod moves them (EliteCreaturesPack
    /// Crossbow/XbowPlayerRig): the string's middle at its rest once shot, onto the right fingers as they hook it in the
    /// reload and riding them into the nut; a bolt in the right fist from the reach to the hip until it is laid, then in
    /// the groove; the right hand kept on the crossbow first (<see cref="XbowPlayerHandPreview"/>); at the shot the string
    /// snaps to its rest and shivers there and the bolt flies out of the groove (in game the game's own bolt projectile
    /// flies from there). Timed by the reload clip's own time in the animator and by the preview's loaded flag.
    /// </summary>
    public sealed class XbowPlayerRigPreview
    {
        private const float Hook = 0.1f * XbowClips.PlayerPace;
        private static readonly float StringGrab = XbowClips.PlayerTime(XbowClips.StringGrab), Spanned = XbowClips.PlayerTime(XbowClips.Spanned);
        private static readonly float BoltGrab = XbowClips.PlayerTime(XbowClips.BoltGrab), Lay = XbowClips.PlayerTime(XbowClips.Lay);

        private readonly Transform tipA, tipB, rest, nut, halfA, halfB, hand;
        private readonly GameObject groove, handBolt, flying;
        private readonly XbowPlayerHandPreview fist;
        private float now, shotAt = -10f;
        private Vector3 flyFrom, flyWay;
        private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>();

        public bool Loaded;   // unloaded until the tour loads it

        public XbowPlayerRigPreview(Transform rig, GameObject player, float scale)
        {
            Transform Part(string name) => rig.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
            (tipA, tipB, rest, nut) = (Part("ecp_xbow_tip_a"), Part("ecp_xbow_tip_b"), Part("ecp_xbow_rest"), Part("ecp_xbow_nut"));
            (halfA, halfB) = (Part("ecp_xbow_string_a"), Part("ecp_xbow_string_b"));
            groove = Part("ecp_xbow_groove_bolt").gameObject;
            groove.transform.localScale = Vector3.one / scale;   // the game bone bolt's length in the 1.25 crossbow
            hand = GreataxePlayer.Bone(player, "RightHand_Attach");
            handBolt = XbowBolt.In(hand, "ecp_xbow_hand_bolt", 1f);
            handBolt.transform.localPosition = new Vector3(0f, 0f, -0.12f);
            flying = XbowBolt.In(null, "ecp_xbow_flying_bolt", 1f);
            flying.transform.SetParent(player.transform, true);
            flying.SetActive(false);
            fist = XbowPlayerHandPreview.Of(player.transform);
        }

        /// <summary>The bolt's renderers too, for the recording.</summary>
        public GameObject Flying => flying;

        /// <summary>The shot: the string lets go and the bolt leaves the groove along the stock.</summary>
        public void Shot()
        {
            (Loaded, shotAt) = (false, now);
            (flyFrom, flyWay) = (groove.transform.position, groove.transform.forward);
        }

        /// <summary>Call after each animator update.</summary>
        public void Update(Animator animator)
        {
            now += 1f / 30f;
            float time = ReloadTime(animator);
            fist?.Apply(tipA.parent, rest.position, nut.position, time);
            Vector3 middle = Middle(time);
            Fly();
            Stretch(halfA, tipA, middle);
            Stretch(halfB, tipB, middle);
            groove.SetActive(time >= 0f ? time >= Lay : Loaded);
            handBolt.SetActive(time >= BoltGrab && time < Lay);
            Drop(time);
        }

        /// <summary>The bolt in the fist swinging from the fingers into the groove as the hand comes down (the mod's XbowPlayerRig.Drop).</summary>
        private void Drop(float time)
        {
            if (!handBolt.activeSelf)
                return;
            float laid = XbowPlayerHandPreview.Laid(time);
            Vector3 held = hand.TransformPoint(new Vector3(0f, 0f, -0.12f));
            handBolt.transform.SetPositionAndRotation(Vector3.Lerp(held, groove.transform.position, laid),
                Quaternion.Slerp(hand.rotation, groove.transform.rotation, laid));
        }

        private float ReloadTime(Animator animator)
        {
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                animator.GetCurrentAnimatorClipInfo(layer, clips);
                if (clips.Count > 0 && clips[0].clip != null && clips[0].clip.name == XbowClips.PlayerReloadName)
                    return Mathf.Clamp01(animator.GetCurrentAnimatorStateInfo(layer).normalizedTime) * clips[0].clip.length;
            }
            return -1f;
        }

        private Vector3 Middle(float time)
        {
            if (time < 0f)
                return Loaded ? nut.position : Shiver(now - shotAt);
            if (time < StringGrab - Hook)
                return rest.position;
            if (time < StringGrab)
                return Vector3.Lerp(rest.position, hand.position, (time - (StringGrab - Hook)) / Hook);
            return time < Spanned ? hand.position : nut.position;
        }

        private Vector3 Shiver(float since) =>
            rest.parent.TransformPoint(rest.localPosition + Vector3.forward * (0.03f * Mathf.Exp(-14f * since) * Mathf.Cos(70f * since)));

        /// <summary>The loosed bolt flying on along the stock, at 20 m/s (slower than the game's, to be seen) for 0.6 s.</summary>
        private void Fly()
        {
            float since = now - shotAt;
            flying.SetActive(since >= 0f && since < 0.6f);
            flying.transform.SetPositionAndRotation(flyFrom + flyWay * (20f * since), Quaternion.LookRotation(flyWay, Vector3.up));
        }

        private static void Stretch(Transform half, Transform tip, Vector3 middle)
        {
            Vector3 from = tip.localPosition, to = half.parent.InverseTransformPoint(middle);
            half.localPosition = from;
            half.localRotation = Quaternion.LookRotation(to - from, Vector3.up);
            half.localScale = new Vector3(1f, 1f, (to - from).magnitude);
        }
    }
}
