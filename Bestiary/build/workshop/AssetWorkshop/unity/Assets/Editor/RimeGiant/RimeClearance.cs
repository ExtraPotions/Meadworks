using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// Measures, rather than eyeballs, whether the Troll's own clips put its body through a plate: samples every clip
    /// every 0.1 s and counts the skin vertices inside each plate's mesh (a ray's signed crossings of the plate's closed,
    /// outward-facing pieces), leaving out the skin the plate sits on (inside it in the idle, where its back is sunk into
    /// the skin). Also logs how low each plate gets, since the ground is at zero, beside how low the skin gets then (a
    /// clip that puts the troll's own fists through the ground takes its forearm plates with them).
    /// </summary>
    public static class RimeClearance
    {
        private sealed class Plate
        {
            public MeshFilter Mesh;
            public Vector3[] Vertices;
            public int[] Triangles;
            public HashSet<int> Own;
            public int Worst;
            public string Where = "-";
            public float Lowest = float.MaxValue;
            public string LowestWhere = "-";
        }

        public static void Report(GameObject troll, RimePoser poser, RimeDress dress)
        {
            dress.Broken(0);
            poser.Pose("Idle", 0f);
            var idle = new RimeBody(troll);
            var plates = dress.Plates.Select(p => Measure(p, idle)).ToList();
            foreach (string state in RimePoser.States)
            {
                for (float t = 0f; t <= poser.Length(state); t += 0.1f)
                {
                    poser.Pose(state, t);
                    var body = new RimeBody(troll);
                    foreach (Plate plate in plates)
                        Check(plate, body, $"{state} at {t:0.0}s");
                }
            }
            for (int i = 0; i < plates.Count; i++)
                Log.Info($"plate {i} clearance: worst {plates[i].Worst} skin vertices inside ({plates[i].Where}); " +
                         $"lowest {plates[i].Lowest:F2} m ({plates[i].LowestWhere})");
        }

        private static Plate Measure(Transform part, RimeBody idle)
        {
            var mesh = part.GetComponentInChildren<MeshFilter>();
            var plate = new Plate { Mesh = mesh, Vertices = mesh.sharedMesh.vertices, Triangles = mesh.sharedMesh.triangles };
            plate.Own = new HashSet<int>(Inside(plate, idle.Vertices));
            return plate;
        }

        private static void Check(Plate plate, RimeBody body, string where)
        {
            int count = Inside(plate, body.Vertices).Count(i => !plate.Own.Contains(i));
            if (count > plate.Worst)
            {
                plate.Worst = count;
                plate.Where = where + " (" + string.Join(", ", Inside(plate, body.Vertices).Where(i => !plate.Own.Contains(i))
                    .Select(i => body.VertexBone[i]).Distinct().Take(3)) + ")";
            }
            float lowest = plate.Vertices.Min(v => plate.Mesh.transform.TransformPoint(v).y);
            if (lowest < plate.Lowest)
            {
                plate.Lowest = lowest;
                plate.LowestWhere = $"{where}, the skin's lowest then {body.Vertices.Min(v => v.y):F2} m";
            }
        }

        /// <summary>The indices of the points inside the plate's mesh.</summary>
        private static IEnumerable<int> Inside(Plate plate, Vector3[] points)
        {
            Transform frame = plate.Mesh.transform;
            Bounds box = plate.Mesh.sharedMesh.bounds;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 local = frame.InverseTransformPoint(points[i]);
                if (box.Contains(local) && Crossings(plate, local) != 0)
                    yield return i;
            }
        }

        /// <summary>Exits minus entries along +X from the point: how many of the plate's closed pieces hold it (either sign).</summary>
        private static int Crossings(Plate plate, Vector3 point)
        {
            int sum = 0;
            for (int i = 0; i < plate.Triangles.Length; i += 3)
            {
                Vector3 a = plate.Vertices[plate.Triangles[i]], b = plate.Vertices[plate.Triangles[i + 1]], c = plate.Vertices[plate.Triangles[i + 2]];
                if (CrossesX(point, a, b, c))
                    sum += Vector3.Cross(b - a, c - a).x > 0f ? 1 : -1;
            }
            return sum;
        }

        /// <summary>Whether the ray from the point along +X passes through the triangle (Moller-Trumbore).</summary>
        private static bool CrossesX(Vector3 origin, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, p = Vector3.Cross(Vector3.right, ac);
            float det = Vector3.Dot(ab, p);
            if (Mathf.Abs(det) < 1e-12f)
                return false;
            Vector3 t = origin - a;
            float u = Vector3.Dot(t, p) / det;
            Vector3 q = Vector3.Cross(t, ab);
            float w = Vector3.Dot(Vector3.right, q) / det;
            return u >= 0f && w >= 0f && u + w <= 1f && Vector3.Dot(ac, q) / det > 0f;
        }
    }
}
