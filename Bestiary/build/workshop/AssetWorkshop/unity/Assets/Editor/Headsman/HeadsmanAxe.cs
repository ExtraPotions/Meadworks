using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// The bone greataxe (assets/ecp_bone_greataxe, staged by assets/ecp_headsman/build.ps1) and the points on it the
    /// clips hold. In the axe's own frame (Unity axes, from Blender's Z up and front -Y): the butt at the origin, the
    /// haft up +Y through an S-curve to the head at 1.5 m, the blade out along -X, its faces towards +-Z. The grips are
    /// the two seats with shorter processes, on the haft's centre line (the model's spine_point, Blender (x, y, z) as
    /// Unity (-x, z, -y)): the right fist holds the upper one and carries the axe, the left fist the lower one.
    /// </summary>
    public static class HeadsmanAxe
    {
        public const string Asset = "ecp_bone_greataxe";
        public const string Folder = HeadsmanBuild.Folder + "/" + Asset;
        public const float Length = 1.5f;
        public const float UpperHeight = 0.73f;
        public const float LowerHeight = 0.34f;

        /// <summary>The upper grip, where the right fist holds the axe.</summary>
        public static readonly Vector3 Upper = Spine(UpperHeight);

        /// <summary>The lower grip, where the left fist holds it.</summary>
        public static readonly Vector3 Lower = Spine(LowerHeight);

        /// <summary>The middle of the blade's cutting edge, what strikes the ground in a slam; measured by <see cref="Report"/>.</summary>
        public static readonly Vector3 Edge = new Vector3(-0.54f, 1.2f, 0f);

        public static string Prefab => Folder + "/" + Asset + ".prefab";

        /// <summary>The haft's centre at a height, as the model builds it (Blender's spine_point turned into Unity axes).</summary>
        public static Vector3 Spine(float height)
        {
            float t = Mathf.Clamp01((height - 0.065f) / 1.102f);
            return new Vector3(-0.040f * Mathf.Sin(t * 2f * Mathf.PI), height, -0.012f * Mathf.Sin(t * Mathf.PI));
        }

        /// <summary>The haft's direction at a height, butt to head.</summary>
        public static Vector3 Along(float height) => (Spine(height + 0.002f) - Spine(height - 0.002f)).normalized;

        public static string Stage()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return PrefabBuilder.Build(Folder);
        }

        /// <summary>Logs the axe's bounds and, at each grip, the model's centre line against the mesh's own middle there.</summary>
        public static void Report()
        {
            var axe = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            Vector3[] points = axe.GetComponentsInChildren<MeshFilter>()
                .SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).ToArray();
            Object.DestroyImmediate(axe);
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 p in points)
                bounds.Encapsulate(p);
            Log.Info($"axe bounds {bounds.min:F3} .. {bounds.max:F3}");
            foreach (float h in new[] { LowerHeight, UpperHeight, 1.0f, 1.2f, 1.35f })
            {
                Vector3[] ring = points.Where(p => Mathf.Abs(p.y - h) < 0.012f).ToArray();
                Vector3 min = ring.Aggregate(Vector3.Min), max = ring.Aggregate(Vector3.Max);
                Log.Info($"axe at {h:F2}: spine {Spine(h):F3}, mesh {min:F3} .. {max:F3}");
            }
        }
    }
}
