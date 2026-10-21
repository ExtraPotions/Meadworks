using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Plays the authored clips through the Skeleton's Animator, as the game does, and logs how far each key's crossbow
    /// lands from where the key put it (the avatar clamps muscles past their limits, and humanoid retargeting is not
    /// exact), where the aim points, and whether the left pinch meets the string's rest, the nut, the quiver's bolt and
    /// the groove at the moments the mod's rig switches them over. Numbers only; the stills are the judge.
    /// </summary>
    public static class XbowCheck
    {
        public static void Run(GameObject skeleton, XbowGrip grip)
        {
            XbowPoser played = XbowBuild.Played(skeleton);
            var watch = new XbowStance(skeleton, played, grip);
            var authored = new XbowStance(skeleton, new XbowPoser(skeleton), grip);
            Keys(XbowClips.AimName, XbowClips.AimKeys(), authored, watch, played);
            Keys(XbowClips.FireName, XbowClips.FireKeys(), authored, watch, played);
            played = XbowBuild.Played(skeleton);
            watch = new XbowStance(skeleton, played, grip);
            Aim(played, watch, XbowClips.AimName, 0.9f);
            Aim(played, watch, XbowClips.FireName, XbowClips.Fire);
            Meet(played, watch, grip, "string rest", XbowClips.StringGrab, bow => XbowStance.At(bow, XbowParts.Rest));
            Meet(played, watch, grip, "nut", XbowClips.Spanned, bow => XbowStance.At(bow, XbowParts.Nut));
            Meet(played, watch, grip, "quiver bolt", XbowClips.BoltGrab, _ => grip.Slot(0).position);
            Meet(played, watch, grip, "groove", XbowClips.Lay, bow => XbowStance.At(bow, XbowParts.Groove));
            Seam(played, watch, (XbowCarry.Clips[0].name, 0f), (XbowClips.AimName, 0f));
            Seam(played, watch, (XbowClips.FireName, XbowClips.Done), (XbowCarry.Clips[0].name, 0f));
        }

        /// <summary>Where one clip hands to another, the crossbow should not jump.</summary>
        private static void Seam(XbowPoser played, XbowStance watch, (string clip, float time) from, (string clip, float time) to)
        {
            played.Pose(from.clip, from.time);
            Pose a = watch.Grip.BowIn(watch);
            played.Pose(to.clip, to.time);
            Pose b = watch.Grip.BowIn(watch);
            Log.Info($"check seam {from.clip} {from.time:0.00} -> {to.clip} {to.time:0.00}: {Vector3.Distance(a.position, b.position) * 100f:F1} cm, "
                     + $"{Quaternion.Angle(a.rotation, b.rotation):F1} deg");
        }

        /// <summary>Each key's crossbow as authored against as played.</summary>
        private static void Keys(string clip, XbowKey[] keys, XbowStance authored, XbowStance watch, XbowPoser played)
        {
            foreach (XbowKey key in keys)
            {
                authored.Apply(key);
                Pose want = authored.Crossbow;
                played.Pose(clip, key.Time);
                Pose got = watch.Grip.BowIn(watch);
                Log.Info($"check {clip} {key.Time:0.00} {key.Bow}/{key.Right}: crossbow off by {Vector3.Distance(want.position, got.position) * 100f:F1} cm, "
                         + $"{Quaternion.Angle(want.rotation, got.rotation):F1} deg");
            }
        }

        private static void Aim(XbowPoser played, XbowStance watch, string clip, float time)
        {
            played.Pose(clip, time);
            Pose bow = watch.Grip.BowIn(watch);
            Vector3 muzzle = XbowStance.At(bow, XbowParts.Muzzle);
            Transform head = watch.Bone("Head");
            Log.Info($"check aim {clip} {time:0.00}: points {bow.forward:F2} ({Vector3.Angle(bow.forward, Vector3.forward):F1} deg off +Z), "
                     + $"up {bow.up:F2}, muzzle {muzzle:F2}, head {head.position:F2}, right hand {watch.Bone("RightHand").position:F2}");
        }

        private static void Meet(XbowPoser played, XbowStance watch, XbowGrip grip, string what, float time, System.Func<Pose, Vector3> point)
        {
            played.Pose(XbowClips.FireName, time);
            Vector3 target = point(grip.BowIn(watch));
            Pose pinch = grip.Pinch(watch);
            Log.Info($"check {what} at {time:0.00}: pinch {pinch.position:F3}, target {target:F3}, off by {Vector3.Distance(pinch.position, target) * 100f:F1} cm");
        }
    }
}
