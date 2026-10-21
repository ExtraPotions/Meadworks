using System.Linq;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// Measures, rather than eyeballs, whether the Greydwarf's own clips put a limb through the satchel: samples the
    /// idle, walk and run every 0.05 s and finds how close the arms and legs come to the satchel's box (its mesh bounds,
    /// in its own frame). Below zero is inside the bag. Limbs are sampled along their bones and given a rough thickness.
    /// <see cref="Search"/> tries other places on the body (each pressed against the body's surface, found by a ray
    /// against the skinned mesh baked in the idle's first frame) and logs each one's worst clearance.
    /// </summary>
    public static class SatchelClearance
    {
        private static readonly string[] Clips = { "Idle", "Dwarf Walk", "Running" };
        private static readonly (string from, string to, float thickness)[] Limbs =
        {
            ("r_arm1", "r_arm2", 0.06f), ("r_arm2", "r_hand", 0.05f), ("r_hand", "r_middle2_end", 0.04f),
            ("l_arm1", "l_arm2", 0.06f), ("l_arm2", "l_hand", 0.05f), ("l_hand", "l_middle2_end", 0.04f),
            ("r_leg1", "r_leg2", 0.09f), ("r_leg2", "r_foot", 0.07f), ("l_leg1", "l_leg2", 0.09f), ("l_leg2", "l_foot", 0.07f),
        };

        /// <summary>(name, bone it hangs from, anchor bone, offset from the anchor, which way the bag's front faces)</summary>
        private static readonly (string name, string mount, string anchor, Vector3 offset, Vector3 facing)[] Candidates =
        {
            ("upper back -0.08 0.04", "spine2", "spine2", new Vector3(-0.08f, 0.04f, 0f), Vector3.back),
            ("upper back -0.08 0.12", "spine2", "spine2", new Vector3(-0.08f, 0.12f, 0f), Vector3.back),
            ("upper back -0.08 0.20", "spine2", "spine2", new Vector3(-0.08f, 0.2f, 0f), Vector3.back),
            ("upper back -0.04 0.04", "spine2", "spine2", new Vector3(-0.04f, 0.04f, 0f), Vector3.back),
            ("upper back -0.04 0.12", "spine2", "spine2", new Vector3(-0.04f, 0.12f, 0f), Vector3.back),
            ("upper back -0.04 0.20", "spine2", "spine2", new Vector3(-0.04f, 0.2f, 0f), Vector3.back),
            ("upper back 0.00 0.20", "spine2", "spine2", new Vector3(0f, 0.2f, 0f), Vector3.back),
            ("upper back -0.04 0.12 tilted", "spine2", "spine2", new Vector3(-0.04f, 0.12f, 0f), new Vector3(0.15f, -0.25f, -1f)),
        };

        public static void Report(GameObject greydwarf)
        {
            Transform satchel = SlingerReference.Bone(greydwarf, "ecr_slinger_satchel");
            foreach (string name in Clips)
            {
                var (gap, where) = Closest(greydwarf, satchel, SlingerReference.Clip(name));
                Log.Info($"satchel clearance in {name}: {gap:+0.00;-0.00} m ({where}){(gap < 0f ? " INSIDE" : "")}");
            }
        }

        public static void Search(GameObject greydwarf)
        {
            Transform satchel = SlingerReference.Bone(greydwarf, "ecr_slinger_satchel");
            AnimationClip idle = SlingerReference.Clip("Idle");
            foreach (var (name, mount, anchor, offset, facing) in Candidates)
            {
                idle.SampleAnimation(greydwarf, 0f);
                satchel.SetParent(SlingerReference.Bone(greydwarf, mount), true);
                satchel.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                satchel.position = SlingBody.Surface(greydwarf, SlingerReference.Bone(greydwarf, anchor).position + offset, facing.normalized);
                string at = $"{satchel.position:F2}";
                var worst = Clips.Select(clip => Closest(greydwarf, satchel, SlingerReference.Clip(clip))).OrderBy(c => c.gap).First();
                Log.Info($"satchel {name} (on {mount}, idle {at}): worst {worst.gap:+0.00;-0.00} m ({worst.where})");
            }
        }

        private static (float gap, string where) Closest(GameObject greydwarf, Transform satchel, AnimationClip clip)
        {
            MeshFilter mesh = satchel.GetComponentInChildren<MeshFilter>();
            (float gap, string where) closest = (float.MaxValue, "");
            for (float t = 0f; t <= clip.length; t += 0.05f)
            {
                clip.SampleAnimation(greydwarf, t);
                foreach (var (from, to, thickness) in Limbs)
                {
                    float gap = Gap(mesh.transform, mesh.sharedMesh.bounds,
                        SlingerReference.Bone(greydwarf, from).position, SlingerReference.Bone(greydwarf, to).position) - thickness;
                    if (gap < closest.gap)
                        closest = (gap, $"{from}-{to} in {clip.name} at {t:0.00}s");
                }
            }
            return closest;
        }

        /// <summary>The smallest distance from points along a bone to the box, in metres (world); negative inside.</summary>
        private static float Gap(Transform mesh, Bounds box, Vector3 a, Vector3 b)
        {
            return Enumerable.Range(0, 9).Select(i => Vector3.Lerp(a, b, i / 8f)).Min(p =>
            {
                Vector3 local = mesh.InverseTransformPoint(p);
                if (box.Contains(local))
                    return -Depth(box, local) * mesh.lossyScale.x;
                return Vector3.Distance(mesh.TransformPoint(box.ClosestPoint(local)), p);
            });
        }

        private static float Depth(Bounds box, Vector3 local)
        {
            Vector3 low = local - box.min, high = box.max - local;
            return Mathf.Min(low.x, low.y, low.z, high.x, high.y, high.z);
        }
    }
}
