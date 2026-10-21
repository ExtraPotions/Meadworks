using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.GameRig
{
    /// <summary>
    /// The editor twin of the mod's BundlePrefabs.CreatureBody.Wear, step for step, for the previews and checks (the
    /// game's scripts are not in this project, so the body renderer is found by the contract's path rather than through
    /// LevelEffects): the creature's body renderer takes our mesh, bind poses and bounds and the creature's own bones by
    /// name in our mesh's order; the sockets it lacks are added under the bones of the same names; its other meshes are
    /// hidden. The material here is our placeholder (the game's Creature shader is only in the game).
    /// </summary>
    public static class GameRigSwap
    {
        public static SkinnedMeshRenderer Wear(GameObject creature, GameObject bundleBody, string rendererPath)
        {
            SkinnedMeshRenderer ours = bundleBody.GetComponentInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer body = GameRigStaging.Body(creature, rendererPath);
            Dictionary<string, Transform> bones = Bones(creature.transform.Find("Visual"));
            int added = AddSockets(bundleBody.transform, bones);
            Rebind(body, ours, bones);
            body.sharedMaterials = ours.sharedMaterials;
            HideOthers(creature, body);
            Log.Info($"swap: {creature.name} wears {ours.sharedMesh.name}, {ours.bones.Length} bones rebound by name, {added} sockets added");
            return body;
        }

        public static void Rebind(SkinnedMeshRenderer body, SkinnedMeshRenderer ours, Dictionary<string, Transform> bones)
        {
            string[] missing = ours.bones.Where(b => !bones.ContainsKey(b.name)).Select(b => b.name).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("the creature lacks bones the body is weighted to: " + string.Join(", ", missing));
            body.sharedMesh = ours.sharedMesh;
            body.bones = ours.bones.Select(b => bones[b.name]).ToArray();
            body.rootBone = bones[ours.rootBone.name];
            body.localBounds = ours.localBounds;
        }

        public static int AddSockets(Transform bundleBody, Dictionary<string, Transform> bones)
        {
            int added = 0;
            foreach (Transform node in bundleBody.GetComponentsInChildren<Transform>(true))
            {
                if (node == bundleBody || bones.ContainsKey(node.name) || node.GetComponent<Renderer>() != null
                    || !bones.TryGetValue(node.parent.name, out Transform parent))
                    continue;
                var copy = new GameObject(node.name).transform;
                copy.SetParent(parent, false);
                copy.localPosition = node.localPosition;
                copy.localRotation = node.localRotation;
                copy.localScale = node.localScale;
                bones[node.name] = copy;
                added++;
            }
            return added;
        }

        public static void HideOthers(GameObject creature, Renderer keep)
        {
            foreach (Renderer renderer in creature.transform.Find("Visual").GetComponentsInChildren<Renderer>(true))
                if (renderer != keep && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    renderer.enabled = false;
        }

        public static Dictionary<string, Transform> Bones(Transform visual)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (Transform node in visual.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(node.name))
                    bones.Add(node.name, node);
            return bones;
        }
    }
}
