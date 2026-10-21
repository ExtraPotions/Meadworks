using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// What the playback measures frame by frame, summed up per phase:
    /// - skeleton: how far our bundle skeleton's bones are from the game creature's under the same controller (0);
    /// - stretch: every edge of our body of 1.5 cm or more as the game's clip bends it, against its rest length. A
    ///   bulky body bends its creases 3 to 4.5 times their length at the clips' extremes (a crotch at a high knee, an
    ///   armpit climbing out of the ground); a vertex on the wrong bone tears the skin by far more, so the check fails
    ///   past 5x or 25 cm longer, and names the worst edge's bones and place;
    /// - ground: the lowest point of our body and of the game's own body, over the phase (feet on the ground).
    /// </summary>
    public sealed class GameRigMotion
    {
        private const float Tearing = 5.0f, Torn = 0.25f, Feet = 0.06f, Skeleton = 0.0005f, Shortest = 0.015f;

        private sealed class Tally
        {
            public string Label;
            public float Gap, Stretch = 1f, Grown, OursLow = float.MaxValue, GameLow = float.MaxValue, OursHigh = float.MinValue;
            public string StretchBone = "";
        }

        private readonly GameRigActors actors;
        private readonly int[] edges;
        private readonly float[] rest;
        private readonly Vector3[] restPoints;
        private readonly Mesh baked = new Mesh();
        private readonly List<Tally> tallies = new List<Tally>();

        public GameRigMotion(GameRigActors actors)
        {
            this.actors = actors;
            edges = Edges(actors.WornBody.sharedMesh.triangles);
            Vector3[] world = GameRigPlacement.World(actors.WornBody, baked);
            rest = Lengths(world);
            restPoints = world.Select(v => actors.Worn.transform.InverseTransformPoint(v)).ToArray();
        }

        public void Begin(string label) => tallies.Add(new Tally { Label = label });

        public void Measure()
        {
            Tally tally = tallies[tallies.Count - 1];
            tally.Gap = Mathf.Max(tally.Gap, actors.SkeletonGap());
            Vector3[] ours = GameRigPlacement.World(actors.WornBody, baked);
            Stretch(tally, ours);
            float low = ours.Min(v => v.y), high = ours.Min(v => v.y);
            tally.OursLow = Mathf.Min(tally.OursLow, low);
            tally.OursHigh = Mathf.Max(tally.OursHigh, high);
            tally.GameLow = Mathf.Min(tally.GameLow, GameRigPlacement.World(actors.GameBody).Min(v => v.y));
        }

        /// <summary>One line per phase, then the three checks over the whole playback.</summary>
        public void Report()
        {
            foreach (Tally t in tallies)
                GameRigReport.Line($"playback {t.Label}: skeleton {t.Gap * 1000:0.000} mm from the game's, worst edge stretch "
                    + $"{t.Stretch:0.00}x, {t.Grown * 100:0.0} cm longer ({t.StretchBone}), lowest point {t.OursLow * 100:0.0} cm (game body {t.GameLow * 100:0.0} cm), "
                    + $"highest low point {t.OursHigh * 100:0.0} cm");
            float gap = tallies.Max(t => t.Gap), stretch = tallies.Max(t => t.Stretch), grown = tallies.Max(t => t.Grown);
            GameRigReport.Check(gap < Skeleton, $"playback: the bundle's skeleton moves as the game creature's under its own controller (worst {gap * 1000:0.000} mm)");
            GameRigReport.Check(stretch < Tearing && grown < Torn, $"playback: no tearing, no edge of {Shortest * 100:0.0} cm or more "
                + $"pulled past {Tearing:0}x its rest length or {Torn * 100:0} cm longer (worst {stretch:0.00}x, {grown * 100:0.0} cm)");
            var standing = tallies.Where(t => t.Label.StartsWith("idle")).ToList();
            float feet = standing.Max(t => Mathf.Abs(t.OursLow - t.GameLow));
            GameRigReport.Check(feet < Feet, $"playback: feet on the ground, standing within {Feet * 100:0} cm of the game body's lowest point (worst {feet * 100:0.0} cm)");
        }

        private void Stretch(Tally tally, Vector3[] world)
        {
            BoneWeight[] weights = actors.WornBody.sharedMesh.boneWeights;
            for (int e = 0; e < rest.Length; e++)
            {
                if (rest[e] < Shortest)
                    continue;
                float length = Vector3.Distance(world[edges[2 * e]], world[edges[2 * e + 1]]);
                if (length / rest[e] > tally.Stretch)
                {
                    tally.Stretch = length / rest[e];
                    tally.Grown = length - rest[e];
                    tally.StretchBone = Describe(edges[2 * e], edges[2 * e + 1], weights);
                }
            }
        }

        /// <summary>The edge's two vertices' main bones and its middle at rest (Blender axes, metres), for finding it.</summary>
        private string Describe(int a, int b, BoneWeight[] weights)
        {
            Vector3 m = (restPoints[a] + restPoints[b]) / 2f;
            string bones = actors.WornBody.bones[weights[a].boneIndex0].name + "/" + actors.WornBody.bones[weights[b].boneIndex0].name;
            return $"{bones} at Blender ({-m.x:0.00}, {-m.z:0.00}, {m.y:0.00})";
        }

        private float[] Lengths(Vector3[] world)
        {
            var lengths = new float[edges.Length / 2];
            for (int e = 0; e < lengths.Length; e++)
                lengths[e] = Vector3.Distance(world[edges[2 * e]], world[edges[2 * e + 1]]);
            return lengths;
        }

        /// <summary>Each edge of the triangles once, as vertex index pairs.</summary>
        private static int[] Edges(int[] triangles)
        {
            var seen = new HashSet<long>();
            var edges = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = triangles[i + k], b = triangles[i + (k + 1) % 3];
                    if (seen.Add(((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b)))
                        edges.AddRange(new[] { a, b });
                }
            return edges.ToArray();
        }
    }
}
