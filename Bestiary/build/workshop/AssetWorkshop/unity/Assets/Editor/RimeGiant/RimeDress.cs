using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.RimeGiant
{
    /// <summary>
    /// The kit on the preview's Troll, hung the way the mod hangs it (each mount on the bone of its name, no offset), with
    /// the mod's two switches: the crust shown only while asleep, and plates broken off from the highest index down.
    /// </summary>
    public sealed class RimeDress
    {
        public readonly Transform[] Plates;
        private readonly Transform[] crust;

        public RimeDress(GameObject troll)
        {
            var kit = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RimeBuild.Folder + "/" + RimeKit.Name + ".prefab"));
            foreach (Transform mount in kit.transform.Cast<Transform>().ToArray())
            {
                mount.SetParent(RimeReference.Bone(troll, mount.name), false);
                mount.localPosition = Vector3.zero;
                mount.localRotation = Quaternion.identity;
                mount.localScale = Vector3.one;
            }
            Object.DestroyImmediate(kit);
            Transform[] parts = troll.GetComponentsInChildren<Transform>(true);
            Plates = Enumerable.Range(0, RimePlates.Count).Select(i => parts.First(t => t.name == RimePlates.Name(i))).ToArray();
            crust = parts.Where(t => t.name.StartsWith("ecr_rime_crust")).ToArray();
        }

        public void Asleep(bool asleep)
        {
            foreach (Transform piece in crust)
                piece.gameObject.SetActive(asleep);
        }

        /// <summary>The first `count` plates to go, as the mod breaks them: the highest index first.</summary>
        public void Broken(int count)
        {
            for (int i = 0; i < Plates.Length; i++)
                Plates[i].gameObject.SetActive(i < Plates.Length - count);
        }
    }
}
