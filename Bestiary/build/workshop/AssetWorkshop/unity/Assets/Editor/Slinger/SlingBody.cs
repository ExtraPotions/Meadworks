using System.Linq;
using UnityEngine;

namespace Workshop.Slinger
{
    /// <summary>
    /// The Greydwarf's body surface in its current pose: the skinned body mesh baked (world-sized, in the renderer's
    /// rotated frame; the renderer's own 100x scale is left out) and a ray tested against every triangle.
    /// </summary>
    public static class SlingBody
    {
        /// <summary>Where a ray from outside, along -outward towards `inside`, first meets the body, a centimetre out; `inside` if it misses.</summary>
        public static Vector3 Surface(GameObject greydwarf, Vector3 inside, Vector3 outward)
        {
            var body = greydwarf.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.bounds.size.sqrMagnitude).First();
            var baked = new Mesh();
            body.BakeMesh(baked, false);
            Vector3[] v = baked.vertices.Select(p => body.transform.position + body.transform.rotation * p).ToArray();
            int[] tris = baked.triangles;
            Vector3 origin = inside + outward * 1.5f;
            float nearest = float.MaxValue;
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (Hit(origin, -outward, v[tris[i]], v[tris[i + 1]], v[tris[i + 2]], out float distance) && distance < nearest)
                    nearest = distance;
            }
            Object.DestroyImmediate(baked);
            return nearest < float.MaxValue ? origin - outward * (nearest - 0.01f) : inside;
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
