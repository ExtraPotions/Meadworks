using UnityEditor;
using UnityEngine;

namespace Workshop.Crossbow
{
    /// <summary>
    /// The players' Bone Crossbow (EliteCreaturesPack Crossbow/XbowItem) as the game holds its crossbows: two models the
    /// mod puts where the game's Arbalest has its "Unloaded" and "Loaded" children, which the game's WeaponLoadState
    /// swaps as the player reloads and shoots.
    ///
    ///   ecp_xbow_item_unloaded   the crossbow, its string let go (straight between the prod tips)
    ///   ecp_xbow_item_loaded     the crossbow spanned (the string in the nut) with a blunt bolt in the groove
    ///   ecp_xbow_item_rig        the crossbow with its string in two halves and a bolt in the groove, and empty markers
    ///                            at the prod tips, the string's rest and the nut: the mod moves the string's middle and
    ///                            shows the bolt as the player reloads (EliteCreaturesPack Crossbow/XbowPlayerRig)
    ///
    /// Each model's origin is the fore-stock's middle (<see cref="XbowParts.Support"/>), front +Z and groove up +Y: the
    /// Arbalest's attach frame puts the player's left hand at its origin with the crossbow pointing +Z and its prod up,
    /// so the left hand holds our fore-stock, our prod lands where the Arbalest's is and our grip near its trigger.
    /// </summary>
    public static class XbowItemModels
    {
        public const string Unloaded = "ecp_xbow_item_unloaded";
        public const string Loaded = "ecp_xbow_item_loaded";
        public const string Rig = "ecp_xbow_item_rig";

        public static string[] Build(string folder) => new[] { Model(folder, Unloaded, false), Model(folder, Loaded, true), RigModel(folder) };

        /// <summary>The crossbow, its string halves at rest, the groove bolt and the markers the mod moves them by.</summary>
        private static string RigModel(string folder)
        {
            var root = new GameObject(Rig);
            GameObject crossbow = Part(folder, "ecp_xbow_crossbow", root.transform);
            crossbow.transform.localPosition = -XbowParts.Support;
            foreach (var (name, at) in new[] { ("ecp_xbow_tip_a", XbowParts.TipA), ("ecp_xbow_tip_b", XbowParts.TipB), ("ecp_xbow_rest", XbowParts.Rest), ("ecp_xbow_nut", XbowParts.Nut) })
            {
                var marker = new GameObject(name).transform;
                marker.SetParent(crossbow.transform, false);
                marker.localPosition = at;
            }
            String(folder, crossbow.transform, "ecp_xbow_string_a", XbowParts.TipA, XbowParts.Rest);
            String(folder, crossbow.transform, "ecp_xbow_string_b", XbowParts.TipB, XbowParts.Rest);
            GameObject bolt = XbowBolt.In(crossbow.transform, "ecp_xbow_groove_bolt", 1f);
            bolt.transform.localPosition = XbowParts.Groove;
            string path = $"{folder}/{Rig}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Log.Info("item model " + path);
            return path;
        }

        private static string Model(string folder, string name, bool loaded)
        {
            var root = new GameObject(name);
            GameObject crossbow = Part(folder, "ecp_xbow_crossbow", root.transform);
            crossbow.transform.localPosition = -XbowParts.Support;
            Vector3 middle = loaded ? XbowParts.Nut : XbowParts.Rest;
            String(folder, crossbow.transform, "string_a", XbowParts.TipA, middle);
            String(folder, crossbow.transform, "string_b", XbowParts.TipB, middle);
            if (loaded)
                XbowBolt.In(crossbow.transform, "bolt", 1f).transform.localPosition = XbowParts.Groove;
            string path = $"{folder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Log.Info("item model " + path);
            return path;
        }

        /// <summary>One half of the string, the one-metre cord stretched from its prod tip to the middle.</summary>
        private static void String(string folder, Transform crossbow, string name, Vector3 from, Vector3 to)
        {
            GameObject half = Part(folder, "ecp_xbow_string", crossbow);
            half.name = name;
            half.transform.localPosition = from;
            half.transform.localRotation = Quaternion.LookRotation(to - from, Vector3.up);
            half.transform.localScale = new Vector3(1f, 1f, (to - from).magnitude);
        }

        private static GameObject Part(string folder, string asset, Transform parent)
        {
            var part = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{asset}/{asset}.prefab"), parent, false);
            part.name = asset;
            return part;
        }
    }
}
