using System;
using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.BundlePrefabs;
using ExtraPotions.Mantle.Embedded.PlateColumn;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mantle.Crafting
{
    /// <summary>
    /// A crafted item's prefab (a backpack or a tacklebox): a copy of the game's Troll hide item (net view, rigidbody,
    /// sync, the ground sparkle) with the item's model in place of the hide, centred, a box collider around it, and its
    /// own shared data: name, description, icon (<c>assets/&lt;group&gt;_&lt;word&gt;.png</c>, rendered from the model), the
    /// kind's item type (a backpack is a Utility item, a tacklebox a Misc item), one per stack, the kind's weight,
    /// teleportable. Only the copy's shared data changes: the game's Troll hide keeps its own.
    /// </summary>
    public static class CraftedItem
    {
        public const string BaseItem = "TrollHide";

        public static GameObject Build(GameObject hide, GameObject model, CraftedKind kind)
        {
            GameObject item = PrefabBench.Copy(hide, kind.Id);
            Transform old = item.transform.Find("model");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            Fit(item, model);
            Describe(item.GetComponent<ItemDrop>().m_itemData.m_shared, kind);
            return item;
        }

        /// <summary>The model under the item, centred on it, inside a box collider and in the item's LOD group.</summary>
        private static void Fit(GameObject item, GameObject model)
        {
            model.transform.SetParent(item.transform, false);
            Bounds bounds = MeshBounds(model, item.transform);
            model.transform.localPosition -= bounds.center;
            item.AddComponent<BoxCollider>().size = bounds.size;
            LODGroup group = item.GetComponent<LODGroup>();
            if (group == null)
                return;
            LOD[] lods = group.GetLODs();
            if (lods.Length == 0)
                return;
            lods[0].renderers = model.GetComponentsInChildren<Renderer>(true);
            group.SetLODs(lods);
        }

        private static void Describe(ItemDrop.ItemData.SharedData shared, CraftedKind kind)
        {
            shared.m_name = kind.Token;
            shared.m_description = kind.DescriptionToken;
            shared.m_itemType = kind.ItemType;
            shared.m_maxStackSize = 1;
            shared.m_maxQuality = kind.MaxQuality;
            shared.m_weight = kind.Weight;
            shared.m_value = 0;
            shared.m_teleportable = true;
            Sprite icon = Icon(kind);
            if (icon != null)
                shared.m_icons = new[] { icon };
        }

        /// <summary>The embedded icon, or null (the Troll hide's icon stays) when it is missing or cannot be read.</summary>
        private static Sprite Icon(CraftedKind kind)
        {
            try
            {
                if (typeof(CraftedItem).Assembly.GetManifestResourceInfo(kind.IconResource) != null)
                    return EmbeddedSprite.Load(typeof(CraftedItem).Assembly, kind.IconResource, $"Mantle_{kind.Group}_{kind.Word}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"the {kind.Name}'s icon could not be read: {e.Message}");
            }
            return null;
        }

        /// <summary>The meshes' bounds in the item's space (renderer bounds are empty while the prefab is inactive).</summary>
        private static Bounds MeshBounds(GameObject model, Transform space)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 corner in Corners(filter.sharedMesh.bounds))
                {
                    Vector3 point = toSpace.MultiplyPoint3x4(corner);
                    if (first)
                        bounds = new Bounds(point, Vector3.zero);
                    bounds.Encapsulate(point);
                    first = false;
                }
            }
            return first ? new Bounds(Vector3.zero, Vector3.one * 0.4f) : bounds;
        }

        private static IEnumerable<Vector3> Corners(Bounds box)
        {
            for (int i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? box.min.x : box.max.x, (i & 2) == 0 ? box.min.y : box.max.y, (i & 4) == 0 ? box.min.z : box.max.z);
        }
    }
}
