using UnityEditor;
using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// The crossbowman's own bolt (assets/ecp_xbow_bolt: nock end at its origin, head along +Z), stood in a slot at its
    /// own size. The kit stands one in the groove, one in the right hand's pinch and one in each quiver slot, as the mod
    /// shows them; the preview's flying bolt is one too, as the mod puts it on the bolt projectile.
    /// </summary>
    public static class XbowBolt
    {
        public const string Asset = "ecp_xbow_bolt";

        public static GameObject Prefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{XbowBuild.Folder}/{Asset}/{Asset}.prefab");

        /// <summary>A bolt in the slot, at `scale` of the slot's own frame (1 in a world-sized part, 1/95 under a bone).</summary>
        public static GameObject In(Transform slot, string name, float scale)
        {
            var bolt = (GameObject)Object.Instantiate(Prefab(), slot, false);
            bolt.name = name;
            bolt.transform.localScale = Vector3.one * scale;
            return bolt;
        }
    }
}
