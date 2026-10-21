using System;
using System.Collections.Generic;
using UnityEngine;

namespace Workshop.Kraken
{
    /// <summary>
    /// Poses the prefabs for the previews the way the mod will: rotations on the head's bones, and for a tentacle every
    /// bone's world position and rotation set along a curve (each bone's +Z along the curve, its -Y, the suckers, on the
    /// side the caller asks for).
    /// </summary>
    public static class KrakenPose
    {
        public static Transform Bone(GameObject g, string name) =>
            KrakenCheck.Find(g, name) ?? throw new InvalidOperationException("no bone " + name);

        public static void Rest(GameObject g)
        {
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
                if (t != g.transform)
                {
                    t.localRotation = Quaternion.identity;
                    t.localScale = Vector3.one;
                }
        }

        /// <summary>Opens the jaws: the upper up, the lower down, about the head's X axis.</summary>
        public static void Beak(GameObject head, float degrees)
        {
            Bone(head, "kh_beak_upper").localRotation = Quaternion.Euler(-degrees, 0, 0);
            Bone(head, "kh_beak_lower").localRotation = Quaternion.Euler(degrees, 0, 0);
        }

        /// <summary>Leans the head forward (positive) or back about kh_neck.</summary>
        public static void Neck(GameObject head, float degrees) => Bone(head, "kh_neck").localRotation = Quaternion.Euler(degrees, 0, 0);

        /// <summary>Bends the column: each of kh_body_1..3 turned by the same rotation (Euler degrees) in its parent.</summary>
        public static void Body(GameObject head, Vector3 euler)
        {
            foreach (string bone in KrakenContract.BodyBones)
                Bone(head, bone).localRotation = Quaternion.Euler(euler);
        }

        /// <summary>Curls a tentacle in place: joint i bends start + step * i degrees towards -Y (suckers inside).</summary>
        public static void Curl(GameObject tentacle, float start, float step)
        {
            for (int i = 1; i < KrakenContract.TentacleBones; i++)
                Bone(tentacle, KrakenContract.TentacleBone(i)).localRotation = Quaternion.Euler(start + step * i, 0, 0);
        }

        /// <summary>
        /// Lays a tentacle along a Catmull-Rom curve through world points, its base on the first. `up(point, tangent)`
        /// gives the direction the top (+Y, away from the suckers) should face there.
        /// </summary>
        public static void Follow(GameObject tentacle, IList<Vector3> points, Func<Vector3, Vector3, Vector3> up)
        {
            List<Vector3> path = Dense(points);
            for (int i = 0; i < KrakenContract.TentacleBones; i++)
            {
                Vector3 p = At(path, KrakenContract.BoneLength * i), q = At(path, KrakenContract.BoneLength * (i + 1));
                Vector3 forward = (q - p).normalized;
                Vector3 hint = up(p, forward);
                hint -= forward * Vector3.Dot(hint, forward);
                if (hint.sqrMagnitude < 1e-4f)
                    hint = Vector3.Cross(forward, Vector3.right);
                Transform bone = Bone(tentacle, KrakenContract.TentacleBone(i));
                bone.SetPositionAndRotation(p, Quaternion.LookRotation(forward, hint));
            }
        }

        private static List<Vector3> Dense(IList<Vector3> points)
        {
            var path = new List<Vector3>();
            for (int k = 0; k < points.Count - 1; k++)
            {
                Vector3 p0 = points[Mathf.Max(k - 1, 0)], p1 = points[k], p2 = points[k + 1], p3 = points[Mathf.Min(k + 2, points.Count - 1)];
                for (int s = 0; s < 40; s++)
                {
                    float t = s / 40f;
                    path.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t
                                     + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t));
                }
            }
            path.Add(points[points.Count - 1]);
            return path;
        }

        /// <summary>The point `distance` metres along the polyline; past its end it carries on straight.</summary>
        private static Vector3 At(List<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float step = Vector3.Distance(path[i - 1], path[i]);
                if (distance <= step)
                    return Vector3.Lerp(path[i - 1], path[i], step > 0 ? distance / step : 0f);
                distance -= step;
            }
            Vector3 last = path[path.Count - 1], before = path[path.Count - 2];
            return last + (last - before).normalized * distance;
        }
    }
}
