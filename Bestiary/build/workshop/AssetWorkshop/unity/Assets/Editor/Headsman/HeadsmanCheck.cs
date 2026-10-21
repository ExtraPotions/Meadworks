using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.Headsman
{
    /// <summary>
    /// Plays each clip through the Skeleton's Animator, as the game will, at 60 frames a second (between the clip's own
    /// keys too, where the muscles are interpolated), and logs how well it keeps what the keys asked: the axe (carried by the right fist) against the key's axe, and the left fist against the axe's lower
    /// seat, worst and mean over the clip; for strikes, how high the edge is at the impact; and the seam between the
    /// carry idle and each attack's first and last frame. Numbers only; the stills are the judge.
    /// </summary>
    public static class HeadsmanCheck
    {
        public static void Run(GameObject skeleton, HeadsmanGrip grip, HeadsmanMove[] moves, XbowPoser played)
        {
            foreach (HeadsmanMove move in moves)
                Clip(skeleton, grip, move, played);
            foreach (HeadsmanMove move in moves)
            {
                Seam(skeleton, grip, played, (HeadsmanAuthor.CarryClips[0].name, 0f), (move.Clip, 0f));
                Seam(skeleton, grip, played, (move.Clip, move.Length), (HeadsmanAuthor.CarryClips[0].name, 0f));
            }
            skeleton.transform.rotation = Quaternion.identity;
        }

        private static void Clip(GameObject skeleton, HeadsmanGrip grip, HeadsmanMove move, XbowPoser played)
        {
            float worstAxe = 0f, worstLeft = 0f, sumLeft = 0f, worstAngle = 0f, at = 0f;
            int count = 0;
            for (int frame = 0; frame <= move.Length * 60f; frame++, count++)
            {
                float t = frame / 60f;
                HeadsmanPose pose = move.Keys.At(t);
                skeleton.transform.rotation = Quaternion.AngleAxis(pose.RootYaw, Vector3.up);
                played.Pose(move.Clip, t);
                Pose axe = grip.AxeIn(XbowReference.Bone(skeleton, "RightHand"));
                Pose want = HeadsmanGrip.Axe(pose.Axe.Turned(pose.RootYaw + pose[Ch.Yaw]));
                float off = Vector3.Distance(axe.position, want.position);
                if (off > worstAxe)
                    (worstAxe, at) = (off, t);
                worstAngle = Mathf.Max(worstAngle, Quaternion.Angle(axe.rotation, want.rotation));
                float left = pose[Ch.LeftFree] > 0.01f ? 0f : Left(skeleton, grip, axe);
                worstLeft = Mathf.Max(worstLeft, left);
                sumLeft += left;
            }
            Log.Info($"check {move.Clip}: axe off by up to {worstAxe * 100f:F1} cm (at {at:0.00} s), {worstAngle:F0} deg; "
                     + $"left fist off its seat up to {worstLeft * 100f:F1} cm, mean {sumLeft / count * 100f:F1} cm");
            if (move.Impact >= 0f)
                Impact(skeleton, grip, move, played);
        }

        /// <summary>How far the left fist's grip is from the axe's lower seat.</summary>
        private static float Left(GameObject skeleton, HeadsmanGrip grip, Pose axe)
        {
            Transform hand = XbowReference.Bone(skeleton, "LeftHand");
            Vector3 fist = hand.TransformPoint(grip.Left.position);
            return Vector3.Distance(fist, HeadsmanGrip.SeatOn(axe, HeadsmanAxe.LowerHeight).position);
        }

        private static void Impact(GameObject skeleton, HeadsmanGrip grip, HeadsmanMove move, XbowPoser played)
        {
            skeleton.transform.rotation = Quaternion.AngleAxis(move.Keys.RootYaw(move.Impact), Vector3.up);
            played.Pose(move.Clip, move.Impact);
            Pose axe = grip.AxeIn(XbowReference.Bone(skeleton, "RightHand"));
            Vector3 edge = axe.position + axe.rotation * HeadsmanAxe.Edge;
            Log.Info($"check {move.Clip} impact at {move.Impact:0.00} s: edge at {edge:F2} (keys {HeadsmanArcs.EdgeOf(move.Keys.At(move.Impact).Axe):F2})");
        }

        private static void Seam(GameObject skeleton, HeadsmanGrip grip, XbowPoser played, (string clip, float time) from, (string clip, float time) to)
        {
            skeleton.transform.rotation = Quaternion.identity;
            played.Pose(from.clip, from.time);
            Pose a = grip.AxeIn(XbowReference.Bone(skeleton, "RightHand"));
            played.Pose(to.clip, to.time);
            Pose b = grip.AxeIn(XbowReference.Bone(skeleton, "RightHand"));
            float cm = Vector3.Distance(a.position, b.position) * 100f;
            if (cm > 2f || Quaternion.Angle(a.rotation, b.rotation) > 3f)
                Log.Info($"check seam {from.clip} {from.time:0.00} -> {to.clip} {to.time:0.00}: {cm:F1} cm, {Quaternion.Angle(a.rotation, b.rotation):F1} deg");
        }
    }
}
