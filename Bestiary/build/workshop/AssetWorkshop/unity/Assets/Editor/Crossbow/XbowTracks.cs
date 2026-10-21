using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// Records where named transforms are frame by frame, for Blender, beside <see cref="XbowCache"/>'s meshes: each one's
    /// world matrix in Blender's axes (C M C^-1 with C the Unity-to-Blender swap (x, y, z) -> (-x, -z, y), so a model
    /// authored in Blender with front -Y can be placed by it directly) and whether it shows, plus the seconds into the
    /// fire clip every frame (below zero outside it). Blender uses them to put other models where the crossbow and its
    /// bolts are - the comparison with another crossbow (assets/ecp_crossbowman/blender_scene.py --compare).
    /// </summary>
    public sealed class XbowTracks
    {
        [Serializable]
        public sealed class Track
        {
            public string name;
            public float[] matrices;
            public bool[] active;
        }

        [Serializable]
        public sealed class TrackData
        {
            public Track[] tracks;
            public float[] fireTimes;
        }

        private static readonly Matrix4x4 ToBlender = new Matrix4x4(
            new Vector4(-1f, 0f, 0f, 0f), new Vector4(0f, 0f, 1f, 0f), new Vector4(0f, -1f, 0f, 0f), new Vector4(0f, 0f, 0f, 1f));

        private readonly List<(string name, Transform transform, List<float> matrices, List<bool> active)> followed =
            new List<(string, Transform, List<float>, List<bool>)>();
        private readonly List<float> fireTimes = new List<float>();

        public void Follow(string name, Transform transform)
        {
            if (transform == null)
                throw new ArgumentException("nothing to follow for " + name);
            followed.Add((name, transform, new List<float>(), new List<bool>()));
        }

        public void Capture(float fireTime)
        {
            foreach (var (_, transform, matrices, active) in followed)
            {
                Matrix4x4 m = ToBlender * transform.localToWorldMatrix * ToBlender.inverse;
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++)
                        matrices.Add(m[row, column]);
                active.Add(transform.gameObject.activeInHierarchy);
            }
            fireTimes.Add(fireTime);
        }

        public void Write(string folder)
        {
            var data = new TrackData
            {
                tracks = followed.Select(f => new Track { name = f.name, matrices = f.matrices.ToArray(), active = f.active.ToArray() }).ToArray(),
                fireTimes = fireTimes.ToArray(),
            };
            File.WriteAllText(Path.Combine(folder, "tracks.json"), JsonUtility.ToJson(data));
            Log.Info($"blender tracks: {followed.Count} transforms, {fireTimes.Count} frames");
        }
    }
}
