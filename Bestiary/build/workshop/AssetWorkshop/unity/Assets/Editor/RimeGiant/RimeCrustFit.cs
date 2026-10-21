using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// The sleeping troll's snow line for the crust: in the Sleeping clip's first frame, a snowflake dropped over every
    /// cell of a 10 cm grid lands on the first skin below it; the cell keeps that height, how much the skin faces up and
    /// which bone carries it (grouped into the crust's mounts). Also logs how far the skin of each mount's part moves
    /// against the spine through the sleeping loop, which decides whether the crust needs more than one piece.
    /// </summary>
    public static class RimeCrustFit
    {
        private const float Spacing = 0.1f;
        private const float Pad = 0.4f;
        private const float Drop = 12f;
        public static readonly string[] Mounts = { "Spine2", "Spine1", "Head", "LeftForeArm", "RightForeArm", "LeftUpLeg", "RightUpLeg" };

        public static CrustFit Measure(GameObject troll, RimePoser poser)
        {
            poser.Pose("Sleeping", 0f);
            var body = new RimeBody(troll);
            Vector3 low = body.Vertices.Aggregate(Vector3.Min) - Vector3.one * Pad, high = body.Vertices.Aggregate(Vector3.Max) + Vector3.one * Pad;
            var fit = new CrustFit
            {
                spacing = Spacing, x0 = low.x, z0 = low.z, mounts = Mounts,
                columns = Mathf.CeilToInt((high.x - low.x) / Spacing) + 1, rows = Mathf.CeilToInt((high.z - low.z) / Spacing) + 1,
            };
            HashSet<int> snowed = Drops(body, fit);
            Crags(body, fit, RimeReference.Bone(troll, "Spine1").position);
            SleepMotion(troll, poser, snowed);
            Log.Info($"crust grid {fit.columns} x {fit.rows} from ({low.x:F2}, {low.z:F2}), {fit.height.Count(h => h > RimeFitData.Miss)} cells on skin, " +
                     $"top {fit.height.Max():F2} m, {fit.crags.Length} crag points");
            return fit;
        }

        /// <summary>Fills the grid; returns the skin vertices the snow lands on.</summary>
        private static HashSet<int> Drops(RimeBody body, CrustFit fit)
        {
            var snowed = new HashSet<int>();
            int count = fit.columns * fit.rows;
            fit.height = new float[count];
            fit.up = new float[count];
            fit.mount = new int[count];
            for (int cell = 0; cell < count; cell++)
            {
                var origin = new Vector3(fit.x0 + cell % fit.columns * Spacing, Drop, fit.z0 + cell / fit.columns * Spacing);
                bool hit = body.Raycast(origin, Vector3.down, null, out float distance, out Vector3 normal, out string bone, out int[] corners);
                fit.height[cell] = hit ? Drop - distance : RimeFitData.Miss;
                fit.up[cell] = hit ? normal.y : 0f;
                fit.mount[cell] = hit ? System.Array.IndexOf(Mounts, MountOf(bone)) : -1;
                if (hit && normal.y > 0.3f && MountOf(bone) != null)
                    snowed.UnionWith(corners);
            }
            return snowed;
        }

        /// <summary>
        /// Points on the skin seen from all around the sleeper, level and from above: rays aimed at the upright through
        /// `centre` (the lower spine), every 20 degrees, at five heights and one steep angle from above. From the front
        /// only the two lowest (the shins and knees): the face stays clear.
        /// </summary>
        private static void Crags(RimeBody body, CrustFit fit, Vector3 centre)
        {
            var points = new List<(Vector3 at, Vector3 normal, int mount)>();
            for (int angle = 0; angle < 360; angle += 20)
            {
                bool front = angle <= 40 || angle >= 320;
                Vector3 across = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                foreach (float y in front ? new[] { 0.9f, 1.5f } : new[] { 0.9f, 1.5f, 2.1f, 2.7f, 3.3f })
                    Crag(body, points, new Vector3(centre.x, y, centre.z) + across * 6f, -across);
                if (front)
                    continue;
                Vector3 high = (across * 0.64f + Vector3.up * 0.77f) * 8f;
                Crag(body, points, new Vector3(centre.x, 2.4f, centre.z) + high, -high.normalized);
            }
            fit.crags = points.Select(p => p.at).ToArray();
            fit.cragNormals = points.Select(p => p.normal).ToArray();
            fit.cragMount = points.Select(p => p.mount).ToArray();
        }

        private static void Crag(RimeBody body, List<(Vector3, Vector3, int)> points, Vector3 origin, Vector3 direction)
        {
            if (!body.Raycast(origin, direction, null, out float distance, out Vector3 normal, out string bone))
                return;
            int mount = System.Array.IndexOf(Mounts, MountOf(bone));
            if (mount >= 0)
                points.Add((origin + direction * distance, normal, mount));
        }

        /// <summary>
        /// The crust mount a skin bone's part belongs to: the head, a forearm, a leg, the lower back, else the upper back
        /// (the shoulders' skin moves least against Spine2 while asleep); null for the hands, which twitch and stay bare.
        /// </summary>
        public static string MountOf(string bone)
        {
            if (bone == "Head" || bone == "Jaw" || bone == "Nose" || bone.StartsWith("L.") || bone.StartsWith("R.") || bone.EndsWith("Eye"))
                return "Head";
            foreach (string side in new[] { "Left", "Right" })
            {
                if (!bone.StartsWith(side))
                    continue;
                string part = bone.Substring(side.Length);
                if (part.StartsWith("Hand"))
                    return null;
                if (part == "Shoulder" || part == "Arm")
                    return "Spine2";
                return part == "ForeArm" ? side + "ForeArm" : side + "UpLeg";
            }
            return bone == "Spine2" ? "Spine2" : "Spine1";
        }

        /// <summary>
        /// Logs, for each mount's snowed skin, the furthest it moves through the sleeping loop against each mount bone
        /// (its own first): a part of the crust belongs on the bone its skin moves least against.
        /// </summary>
        private static void SleepMotion(GameObject troll, RimePoser poser, HashSet<int> snowed)
        {
            poser.Pose("Sleeping", 0f);
            var start = new RimeBody(troll);
            var bones = Mounts.ToDictionary(m => m, m => RimeReference.Bone(troll, m));
            var rest = Mounts.ToDictionary(m => m, m => bones[m].localToWorldMatrix);
            var worst = new Dictionary<(string part, string bone), float>();
            for (float t = 0.1f; t < poser.Length("Sleeping"); t += 0.1f)
            {
                poser.Pose("Sleeping", t);
                Vector3[] now = new RimeBody(troll).Vertices;
                foreach (int i in snowed)
                {
                    foreach (string bone in Mounts)
                    {
                        var key = (MountOf(start.VertexBone[i]) ?? "hands", bone);
                        Vector3 held = rest[bone].MultiplyPoint3x4(bones[bone].worldToLocalMatrix.MultiplyPoint3x4(now[i]));
                        worst[key] = Mathf.Max(worst.TryGetValue(key, out float w) ? w : 0f, (held - start.Vertices[i]).magnitude);
                    }
                }
            }
            poser.Pose("Sleeping", 0f);
            foreach (string part in Mounts.Where(p => worst.ContainsKey((p, p))))
                Log.Info($"sleeping motion of {part}'s snowed skin: " + string.Join(", ", Mounts.OrderBy(b => b != part).Select(b => $"{b} {worst[(part, b)]:F3}")));
        }
    }
}
