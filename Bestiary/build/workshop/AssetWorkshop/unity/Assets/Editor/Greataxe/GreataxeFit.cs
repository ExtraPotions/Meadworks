using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Greataxe
{
    /// <summary>
    /// How the greataxe's haft sits in the hand against the game's Battleaxe, whose haft the game's clips put the left
    /// hand on: both hafts' centre lines in the frame of RightHand_Attach (vertex centroids in thin slices along +Z,
    /// below the heads), and the left palm there frame by frame as the preview plays (<see cref="GreataxeSteps"/>),
    /// logged per part of the preview with its distance from each haft.
    /// </summary>
    public static class GreataxeFit
    {
        private const string BattleaxeMesh = "GameElements/Items/weapons/_res/battleaxe/model/battleaxe.asset";
        private const float Slice = 0.02f, Low = -0.25f, High = 0.5f;

        /// <summary>The Battleaxe model's own place in its attach (Battleaxe.prefab: attach/battleaxe).</summary>
        private static readonly Matrix4x4 Vanilla = Matrix4x4.TRS(Vector3.zero, GreataxeModel.BattleaxeTurn, Vector3.one * 0.3306732f);

        /// <summary>The Battleaxe's haft centre line, (z, centre) per slice.</summary>
        public static List<Vector3> VanillaHaft()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ReferenceAssets.Import(BattleaxeMesh, "Battleaxe"));
            return Line(mesh.vertices.Select(v => Vanilla.MultiplyPoint3x4(v)));
        }

        /// <summary>The greataxe's haft centre line (the model's spine) as <see cref="GreataxeModel"/> hangs it.</summary>
        public static List<Vector3> OurHaft()
        {
            GameObject attach = GreataxeModel.Attach();
            Transform axe = attach.transform.GetChild(0);
            List<Vector3> line = Enumerable.Range(0, 150).Select(i => i * 0.01f)
                .Select(h => attach.transform.InverseTransformPoint(axe.TransformPoint(Workshop.Headsman.HeadsmanAxe.Spine(h)))).ToList();
            Object.DestroyImmediate(attach);
            return line;
        }

        /// <summary>Logs which way the left fist's attach axes lie against the right's +Z (the haft) in each part.</summary>
        public static void Axes(IEnumerable<(string part, Quaternion turn)> turns)
        {
            foreach (var group in turns.GroupBy(t => t.part))
            {
                Vector3 x = Mean(group.Select(t => t.turn * Vector3.right)), y = Mean(group.Select(t => t.turn * Vector3.up)), z = Mean(group.Select(t => t.turn * Vector3.forward));
                Log.Info($"greataxe fit {group.Key}: left fist axes in the right's frame x {x:F2} y {y:F2} z {z:F2}");
            }
        }

        private static Vector3 Mean(IEnumerable<Vector3> v) => v.Aggregate(Vector3.zero, (a, b) => a + b).normalized;

        /// <summary>Logs, per part, where the left palm is and how far it is from each haft's line.</summary>
        public static void Report(IEnumerable<(string part, Vector3 palm)> palms)
        {
            List<Vector3> vanilla = VanillaHaft(), ours = OurHaft();
            Log.Info("greataxe fit: vanilla haft " + Describe(vanilla) + "; ours " + Describe(ours));
            foreach (var group in palms.GroupBy(p => p.part))
            {
                Vector3[] at = group.Select(p => p.palm).ToArray();
                float toVanilla = at.Average(p => Distance(vanilla, p)), toOurs = at.Average(p => Distance(ours, p));
                Vector3 mean = at.Aggregate(Vector3.zero, (a, b) => a + b) / at.Length;
                int off = at.Count(p => Distance(ours, p) > 0.03f);
                Log.Info($"greataxe fit {group.Key}: left palm mean {mean:F3}, z {at.Min(p => p.z):F2}..{at.Max(p => p.z):F2}; "
                    + $"from the Battleaxe haft {toVanilla * 100f:F1} cm, from ours {toOurs * 100f:F1} cm, {off} of {at.Length} frames off ours");
            }
        }

        private static string Describe(List<Vector3> line) =>
            string.Join(" ", line.Where((_, i) => i % 8 == 0).Select(p => $"z{p.z:F2}:({p.x:F3},{p.y:F3})"));

        /// <summary>The centre of the points in each slice from <see cref="Low"/> to <see cref="High"/> along +Z.</summary>
        private static List<Vector3> Line(IEnumerable<Vector3> points)
        {
            Vector3[] all = points.ToArray();
            var line = new List<Vector3>();
            for (float z = Low; z <= High; z += Slice)
            {
                Vector3[] slice = all.Where(p => Mathf.Abs(p.z - z) < Slice / 2f).ToArray();
                if (slice.Length > 0)
                    line.Add(new Vector3(slice.Average(p => p.x), slice.Average(p => p.y), z));
            }
            return line;
        }

        /// <summary>Distance from a point to the line, sideways, at the point's height (the nearest slice).</summary>
        private static float Distance(List<Vector3> line, Vector3 p)
        {
            Vector3 at = line.OrderBy(c => Mathf.Abs(c.z - p.z)).First();
            return new Vector2(p.x - at.x, p.y - at.y).magnitude;
        }
    }
}
