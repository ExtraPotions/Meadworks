using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// The Troll's body surface in its current pose, for rays and clearance: the skinned body mesh baked into world
    /// space (the renderer's own 130x scale is left out, as BakeMesh does without useScale), each vertex tagged with the
    /// bone that weighs most on it. The hair (the grass tufts) is left out: snow and ice sit on the skin under it.
    /// </summary>
    public sealed class RimeBody
    {
        public readonly Vector3[] Vertices;
        public readonly string[] VertexBone;
        private readonly int[] triangles;
        private readonly string[] triangleBone;

        public RimeBody(GameObject troll)
        {
            var body = RimeReference.Bone(troll, "Body").GetComponent<SkinnedMeshRenderer>();
            var baked = new Mesh();
            body.BakeMesh(baked, false);
            Vertices = baked.vertices.Select(p => body.transform.position + body.transform.rotation * p).ToArray();
            triangles = baked.triangles;
            Object.DestroyImmediate(baked);
            string[] bones = body.bones.Select(b => b.name).ToArray();
            VertexBone = body.sharedMesh.boneWeights.Select(w => bones[w.boneIndex0]).ToArray();   // sorted by weight
            triangleBone = Enumerable.Range(0, triangles.Length / 3).Select(Majority).ToArray();
        }

        /// <summary>The bone two or three of the triangle's corners share, else its first corner's.</summary>
        private string Majority(int triangle)
        {
            string a = VertexBone[triangles[triangle * 3]], b = VertexBone[triangles[triangle * 3 + 1]], c = VertexBone[triangles[triangle * 3 + 2]];
            return b == c ? b : a;
        }

        /// <summary>
        /// The first hit along the ray on a triangle of one of `region`'s bones (any bone when null): its distance, its
        /// normal turned to face the ray, and its bone.
        /// </summary>
        public bool Raycast(Vector3 origin, Vector3 direction, ICollection<string> region, out float distance, out Vector3 normal, out string bone) =>
            Raycast(origin, direction, region, out distance, out normal, out bone, out _);

        /// <summary>As above, also giving the corners (vertex indices) of the triangle hit.</summary>
        public bool Raycast(Vector3 origin, Vector3 direction, ICollection<string> region, out float distance, out Vector3 normal, out string bone,
            out int[] corners)
        {
            distance = float.MaxValue;
            int first = -1;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (region != null && !region.Contains(triangleBone[i / 3]))
                    continue;
                if (Hit(origin, direction, Vertices[triangles[i]], Vertices[triangles[i + 1]], Vertices[triangles[i + 2]], out float t) && t < distance)
                {
                    distance = t;
                    first = i;
                }
            }
            corners = first < 0 ? null : new[] { triangles[first], triangles[first + 1], triangles[first + 2] };
            bone = first < 0 ? null : triangleBone[first / 3];
            normal = first < 0 ? Vector3.zero : Facing(first, direction);
            return first >= 0;
        }

        private Vector3 Facing(int first, Vector3 direction)
        {
            Vector3 a = Vertices[triangles[first]], b = Vertices[triangles[first + 1]], c = Vertices[triangles[first + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            return Vector3.Dot(normal, direction) > 0f ? -normal : normal;
        }

        /// <summary>Ray against one triangle (Moller-Trumbore), either side.</summary>
        private static bool Hit(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0f;
            Vector3 ab = b - a, ac = c - a, p = Vector3.Cross(direction, ac);
            float det = Vector3.Dot(ab, p);
            if (Mathf.Abs(det) < 1e-9f)
                return false;
            Vector3 t = origin - a;
            float u = Vector3.Dot(t, p) / det;
            Vector3 q = Vector3.Cross(t, ab);
            float w = Vector3.Dot(direction, q) / det;
            distance = Vector3.Dot(ac, q) / det;
            return u >= 0f && w >= 0f && u + w <= 1f && distance > 0f;
        }
    }
}
