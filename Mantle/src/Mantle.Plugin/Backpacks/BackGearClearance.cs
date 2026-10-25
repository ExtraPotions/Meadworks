// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Mantle.Embedded.BundlePrefabs;
using UnityEngine;

namespace Mantle.Backpacks
{
    internal sealed class BackGearClearance : MonoBehaviour
    {
        private const float Gap = 0.025f;
        private readonly Gear left = new Gear();
        private readonly Gear right = new Gear();
        private Bounds pack;
        private bool applied;

        internal static void Follow(Transform mount, VisEquipment equipment)
        {
            if (mount == null)
                return;
            BackGearClearance clearance = mount.GetComponent<BackGearClearance>();
            if (clearance == null)
            {
                clearance = mount.gameObject.AddComponent<BackGearClearance>();
                clearance.pack = ModelBounds.In(mount.gameObject, mount);
            }
            if (clearance.applied && clearance.left.Matches(equipment.m_leftBackItemInstance)
                && clearance.right.Matches(equipment.m_rightBackItemInstance))
                return;
            clearance.left.Follow(equipment.m_leftBackItemInstance);
            clearance.right.Follow(equipment.m_rightBackItemInstance);
            float distance = Mathf.Max(clearance.Distance(clearance.left), clearance.Distance(clearance.right));
            // Move the native pair together so the shield and sword keep their relative placement.
            Vector3 offset = mount.TransformVector(Vector3.back * distance);
            clearance.left.Move(offset);
            clearance.right.Move(offset);
            clearance.applied = true;
        }

        internal static void Let(Transform mount)
        {
            BackGearClearance clearance = mount.GetComponent<BackGearClearance>();
            if (clearance != null)
                clearance.Release();
        }

        private float Distance(Gear gear)
        {
            if (gear.Item == null || !gear.In(transform, out Bounds item))
                return 0f;
            if (item.max.x <= pack.min.x || item.min.x >= pack.max.x ||
                item.max.y <= pack.min.y || item.min.y >= pack.max.y || item.min.z >= pack.max.z)
                return 0f;
            // Authored packs face -Z in the upright Spine2 mount's metre frame.
            return Mathf.Max(0f, item.max.z - pack.min.z + Gap);
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private void Release()
        {
            applied = false;
            left.Restore();
            right.Restore();
        }

        private sealed class Gear
        {
            internal Transform Item { get; private set; }
            private Transform parent;
            private MeshFilter[] meshes;
            private Vector3 offset;

            internal bool Matches(GameObject instance)
            {
                Transform next = instance != null ? instance.transform : null;
                return Item == next && (next == null || parent == next.parent);
            }

            internal void Follow(GameObject instance)
            {
                Restore();
                Transform next = instance != null ? instance.transform : null;
                if (Item == next && (next == null || parent == next.parent))
                    return;
                Item = next;
                parent = next != null ? next.parent : null;
                if (next != null)
                    meshes = instance.GetComponentsInChildren<MeshFilter>(true);
            }

            internal bool In(Transform space, out Bounds result)
            {
                result = default;
                bool first = true;
                foreach (MeshFilter filter in meshes)
                {
                    if (filter == null || filter.sharedMesh == null)
                        continue;
                    // Project each mesh once; rotating an already combined box inflates the empty space between meshes.
                    Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Bounds bounds = filter.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 point = toSpace.MultiplyPoint3x4(new Vector3(
                            (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                            (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                            (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                        if (first)
                        {
                            result = new Bounds(point, Vector3.zero);
                            first = false;
                        }
                        result.Encapsulate(point);
                    }
                }
                return !first;
            }

            internal void Move(Vector3 worldOffset)
            {
                if (Item == null || parent == null)
                    return;
                offset = parent.InverseTransformVector(worldOffset);
                Item.localPosition += offset;
            }

            internal void Restore()
            {
                if (Item != null && Item.parent == parent)
                    Item.localPosition -= offset;
                offset = Vector3.zero;
            }
        }
    }
}
