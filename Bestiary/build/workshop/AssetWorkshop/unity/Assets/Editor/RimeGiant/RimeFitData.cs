using System;
using System.IO;
using UnityEngine;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// What the fit measures on the Troll and the Blender parts are built to (out/rimegiant/fit.json): each plate's frame
    /// and the skin under it, and the snow line over the sleeping troll. Unity axes and metres throughout; the Blender
    /// side turns them into its own (assets/ecr_rimegiant/rime/fit.py). Regenerated from the reference export, never
    /// committed.
    /// </summary>
    [Serializable]
    public sealed class RimeFitData
    {
        public const float Miss = -99f;
        public PlateFit[] plates;
        public CrustFit crust;

        public static RimeFitData Read(string path) => JsonUtility.FromJson<RimeFitData>(File.ReadAllText(path));

        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }
    }

    /// <summary>
    /// A plate's frame in the idle's first frame (world): +Z out of the skin, +Y up the body or the limb, the origin on
    /// the skin at the plate's middle. `depth` is the skin's height along +Z under a grid of columns x rows points
    /// spanning width x height, row by row from the bottom left; <see cref="RimeFitData.Miss"/> where no skin is there.
    /// </summary>
    [Serializable]
    public sealed class PlateFit
    {
        public int index;
        public string name, mount;
        public Vector3 position;
        public Quaternion rotation;
        public float width, height;
        public int columns, rows;
        public float[] depth;
    }

    /// <summary>
    /// The sleeping troll seen from above (world, the Sleeping clip's first frame): a grid of columns x rows cells of
    /// `spacing` metres from (x0, z0), each with the height of the first skin a falling snowflake meets, how much that
    /// skin faces up (the normal's y), and which of `mounts` carries it. <see cref="RimeFitData.Miss"/> where it misses.
    /// `crags` are points on the skin of its back and sides, seen from around and above, with the skin's normal there
    /// and the mount that carries it: where lumps of rime can cling so the sleeper's outline turns to rock.
    /// </summary>
    [Serializable]
    public sealed class CrustFit
    {
        public float spacing, x0, z0;
        public int columns, rows;
        public float[] height, up;
        public int[] mount;
        public string[] mounts;
        public Vector3[] crags, cragNormals;
        public int[] cragMount;
    }
}
