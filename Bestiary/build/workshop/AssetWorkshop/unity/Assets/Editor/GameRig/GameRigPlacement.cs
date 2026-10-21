using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// Where the body sits on the skeleton, against the game's own body at rest: for every bone both weight (a vertex
    /// counts for the bone it follows most), the centre of its vertices, all reported. The head, hands and feet must lie
    /// within 20 cm of the game body's (a body turned the wrong way or mirrored puts them on the far side of their
    /// bones); the torso may differ as much as the design does (a hump, a belly). Facing is checked plainly too: the
    /// toes and the jaw lie in front (+Z) of the foot and head bones.
    /// </summary>
    public static class GameRigPlacement
    {
        private const float Tolerance = 0.2f;
        private const int Enough = 6;
        private static readonly string[] Extremities = { "Head", "Hand", "Foot", "ToeBase" };

        public static void Check(SkinnedMeshRenderer ours, SkinnedMeshRenderer game)
        {
            Dictionary<string, Vector3> mine = Centres(ours), theirs = Centres(game);
            var gaps = mine.Keys.Where(theirs.ContainsKey).Select(b => (bone: b, gap: Vector3.Distance(mine[b], theirs[b])))
                .OrderByDescending(g => g.gap).ToList();
            GameRigReport.Line("placement: skin centres against the game body's: "
                + string.Join(", ", gaps.Take(6).Select(g => $"{g.bone} {g.gap * 100:0.0} cm")) + $" ... ({gaps.Count} bones)");
            var ends = gaps.Where(g => Extremities.Any(e => g.bone.EndsWith(e))).ToList();
            GameRigReport.Check(ends.Count > 0 && ends[0].gap < Tolerance, $"placement: head, hands and feet within {Tolerance * 100:0} cm "
                + "of the game body's (a turned or mirrored body puts them on the far side of their bones; worst "
                + (ends.Count > 0 ? $"{ends[0].bone} {ends[0].gap * 100:0.0} cm)" : "none found)"));
            Facing(ours, mine, "ours");
            Facing(game, theirs, "the game body's");
        }

        private static void Facing(SkinnedMeshRenderer skin, Dictionary<string, Vector3> centres, string whose)
        {
            var pairs = new[] { ("LeftToeBase", "LeftFoot"), ("RightToeBase", "RightFoot"), ("Jaw", "Head") };
            foreach (var (part, bone) in pairs.Where(p => centres.ContainsKey(p.Item1)))
            {
                Transform joint = skin.bones.FirstOrDefault(b => b.name == bone);
                if (joint == null)
                    continue;
                float ahead = skin.transform.root.InverseTransformPoint(centres[part]).z - skin.transform.root.InverseTransformPoint(joint.position).z;
                GameRigReport.Check(ahead > 0f, $"facing ({whose}): {part} skin {ahead * 100:0.0} cm in front of {bone}");
            }
        }

        /// <summary>The centre of the vertices each bone carries most, in the world, as the renderer draws them now.</summary>
        public static Dictionary<string, Vector3> Centres(SkinnedMeshRenderer skin)
        {
            Vector3[] world = World(skin);
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            var sums = new Dictionary<string, (Vector3 sum, int count)>();
            for (int i = 0; i < world.Length; i++)
            {
                string bone = skin.bones[weights[i].boneIndex0].name;
                sums.TryGetValue(bone, out var s);
                sums[bone] = (s.sum + world[i], s.count + 1);
            }
            return sums.Where(p => p.Value.count >= Enough).ToDictionary(p => p.Key, p => p.Value.sum / p.Value.count);
        }

        /// <summary>The skinned vertices in world space (BakeMesh without scale is world-sized, in the renderer's frame).</summary>
        public static Vector3[] World(SkinnedMeshRenderer skin, Mesh into = null)
        {
            var baked = into ?? new Mesh();
            skin.BakeMesh(baked, false);
            Transform t = skin.transform;
            return baked.vertices.Select(v => t.position + t.rotation * v).ToArray();
        }
    }
}
