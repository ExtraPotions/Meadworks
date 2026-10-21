// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// A broken plate falling away: a local copy of the plate where it was, knocked outwards and up, tumbling on the
    /// ground for a few seconds before it melts away, with the game's ice shattering where it broke. Every machine
    /// makes its own from the plate it was drawing, so nothing of it crosses the network; it never hurts or blocks
    /// anyone for long.
    /// </summary>
    internal static class RimeShards
    {
        private const float Life = 8f;
        private const float Mass = 30f;
        private const float Outward = 3f;
        private const float Upward = 2.5f;

        public static void BreakOff(GameObject plate, Transform giant)
        {
            Transform from = plate.transform;
            RimeEffects.Break(from.position);
            GameObject shard = Object.Instantiate(plate, from.position, from.rotation);
            shard.name = "ecp_rime_shard";
            shard.transform.localScale = from.lossyScale;
            shard.SetActive(true);
            SetLayer(shard, LayerMask.NameToLayer("item"));
            Fit(shard);
            Throw(shard.AddComponent<Rigidbody>(), from.position - (giant.position + Vector3.up * 3f));
            Object.Destroy(shard, Life);
        }

        /// <summary>A box collider around the shard's meshes, in its own space.</summary>
        private static void Fit(GameObject shard)
        {
            Bounds? bounds = null;
            foreach (MeshFilter filter in shard.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }
                Bounds local = Local(shard.transform, filter.transform, filter.sharedMesh.bounds);
                bounds = bounds is Bounds b ? Grown(b, local) : local;
            }
            var box = shard.AddComponent<BoxCollider>();
            box.center = bounds?.center ?? Vector3.zero;
            box.size = Vector3.Max(bounds?.size ?? Vector3.one * 0.5f, Vector3.one * 0.1f);
        }

        private static Bounds Local(Transform root, Transform part, Bounds mesh)
        {
            Vector3 center = root.InverseTransformPoint(part.TransformPoint(mesh.center));
            Vector3 size = root.InverseTransformVector(part.TransformVector(mesh.size));
            return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
        }

        private static Bounds Grown(Bounds a, Bounds b)
        {
            a.Encapsulate(b);
            return a;
        }

        private static void Throw(Rigidbody body, Vector3 away)
        {
            away.y = 0f;
            body.mass = Mass;
            body.linearVelocity = away.normalized * Outward + Vector3.up * Upward;
            body.angularVelocity = Random.insideUnitSphere * 4f;
        }

        private static void SetLayer(GameObject shard, int layer)
        {
            if (layer < 0)
            {
                return;
            }
            foreach (Transform part in shard.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = layer;
            }
        }
    }
}
