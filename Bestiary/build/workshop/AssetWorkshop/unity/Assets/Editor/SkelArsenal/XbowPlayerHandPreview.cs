using System.Linq;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.SkelArsenal
{
    /// <summary>
    /// The preview's copy of EliteCreaturesPack Crossbow/XbowPlayerHand (keep the two alike). A player's right hand on the Bone Crossbow, after each pose (the user: "the right hand isn't even touching the
    /// crossbow"; "when he loads the bolt he should set it down where it goes"): the game's crossbow clips hold the right
    /// fist where the Arbalest's longer stock is, so the fist is brought onto our stock's grip (the crossbow part's origin,
    /// AssetWorkshop Crossbow/XbowParts) whenever the crossbow is not being reloaded. Through the reload, by the reload
    /// clip's own time: from the grip onto the string at its rest, drawing it back into the nut, then free (the clip
    /// takes it to the hip for a bolt), then down to just above the middle of the groove, where the bolt drops in (the
    /// bolt swings from the fingers into the groove, <see cref="XbowPlayerRig"/>; the user: "just quickly plop the bolt
    /// in in a natural way"), a pat, and back to the grip. The fist always keeps the clip's own turn: turning it to lay
    /// the bolt wrung the wrist and crushed the forearm. Two-bone IK on the right arm.
    /// </summary>
    public sealed class XbowPlayerHandPreview
    {
        // The player reload clip's own seconds (AssetWorkshop XbowClips PlayerReloadKeys, paced by XbowClips.PlayerPace for the 2.3 s reload).
        private const float Reach = 0.3981f, StringGrab = 0.7077f, Spanned = 1.2385f, Leave = 1.5215f, Back = 2.1231f, Lay = 2.5477f, Pat = 2.7954f, Done = 3.5031f;
        private static readonly Vector3 Grip = new Vector3(0.018f, -0.012f, -0.01f);   // the fist's middle off the stock's grip
        private static readonly Vector3 OnString = new Vector3(0f, 0.012f, -0.012f);
        private const float OverGroove = 0.3f, Above = 0.07f;   // the fist over the laid bolt's middle, a hand's height up

        private readonly Transform arm, forearm, hand, attach;

        private XbowPlayerHandPreview(Transform arm, Transform forearm, Transform hand, Transform attach) =>
            (this.arm, this.forearm, this.hand, this.attach) = (arm, forearm, hand, attach);

        public static XbowPlayerHandPreview Of(Transform visual)
        {
            Transform Bone(string name) => visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(b => b.name == name);
            Transform a = Bone("RightArm"), f = Bone("RightForeArm"), h = Bone("RightHand"), t = Bone("RightHand_Attach");
            return a == null || f == null || h == null || t == null ? null : new XbowPlayerHandPreview(a, f, h, t);
        }

        /// <summary>
        /// The fist placed for this moment: `bow` the crossbow part (its origin the grip), `rest` and `nut` the string's
        /// middle at rest and spanned, `time` seconds into the reload clip (below 0 when not reloading).
        /// </summary>
        public void Apply(Transform bow, Vector3 rest, Vector3 nut, float time)
        {
            var (target, weight) = Target(bow, rest, nut, time);
            if (weight <= 0.001f)
            {
                return;
            }
            Quaternion held = hand.rotation;
            Vector3 goal = Vector3.Lerp(attach.position, target, weight) - (attach.position - hand.position);
            Vector3 elbow = forearm.position, middle = (arm.position + hand.position) * 0.5f;
            TwoBoneIk.Solve(arm, forearm, hand, goal, elbow + (elbow - middle).normalized * 0.3f);
            hand.rotation = held;
        }

        /// <summary>Where the fist's middle goes, and how fully.</summary>
        private static (Vector3 target, float weight) Target(Transform bow, Vector3 rest, Vector3 nut, float time)
        {
            Vector3 grip = bow.TransformPoint(Grip), onRest = rest + bow.TransformVector(OnString), onNut = nut + bow.TransformVector(OnString);
            Vector3 laying = bow.TransformPoint(Groove) + bow.forward * OverGroove + bow.up * Above;
            if (time < 0f || time >= Done)
            {
                return (grip, 1f);
            }
            if (time < StringGrab)
            {
                return (Vector3.Lerp(grip, onRest, Ease(time, Reach, StringGrab)), 1f);
            }
            if (time < Leave)
            {
                return (Vector3.Lerp(onRest, onNut, Ease(time, StringGrab, Spanned)), 1f - Ease(time, Spanned, Leave));
            }
            if (time < Lay)
            {
                return (laying, Laid(time));
            }
            return time < Pat ? (laying, 1f) : (Vector3.Lerp(laying, grip, Ease(time, Pat, Done)), 1f);
        }

        /// <summary>How far the bolt has come down into the groove, 0 in the hand at the hip to 1 laid.</summary>
        public static float Laid(float time) => Ease(time, Back, Lay);

        private static readonly Vector3 Groove = new Vector3(0f, 0.031f, 0.100f);   // a laid bolt's nock end (XbowParts)

        private static float Ease(float time, float from, float to) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, time));
    }
}
