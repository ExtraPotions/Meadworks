// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// What weapons and arrows hit on the kraken: a sphere round the mantle, a capsule round the face and one down the
    /// body under the head, on the head's bones, and a capsule on every bone of each tentacle, sized to its thickness there. They sit on the game's hitbox
    /// layer, which attacks and projectiles hit but nothing collides with, so a tentacle lying on the deck neither
    /// shoves the ship nor blocks the crew. They ride their bones, so they follow every pose; a hit on any of them is a
    /// hit on the kraken.
    /// </summary>
    public static class KrakenHitboxes
    {
        private const float Margin = 0.05f;

        public static void Add(Transform model)
        {
            int layer = LayerMask.NameToLayer("hitbox");
            Transform? head = model.Find(KrakenBody.HeadName);
            if (head != null)
            {
                Sphere(RigBone.Named(head, "kh_mantle") ?? head, head.TransformPoint(0f, 4.2f, -0.7f), 2.1f, layer);
                Capsule(RigBone.Named(head, "kh_neck") ?? head, head.TransformPoint(0f, 1.7f, 0.3f), head.rotation, 1.5f, 3.4f, 1, layer);
                Capsule(RigBone.Named(head, "kh_body_1") ?? head, head.TransformPoint(0f, -1.7f, 0f), head.rotation, 1.4f, 2.8f, 1, layer);
                Capsule(RigBone.Named(head, "kh_body_3") ?? head, head.TransformPoint(0f, -4.0f, 0f), head.rotation, 1.1f, 2.2f, 1, layer);
            }
            for (int i = 0; i < KrakenBody.Tentacles; i++)
            {
                Transform? arm = model.Find(KrakenBody.TentaclePrefix + i);
                if (arm != null)
                {
                    Tentacle(arm, layer);
                }
            }
        }

        private static void Tentacle(Transform root, int layer)
        {
            for (int i = 0; i < TentacleSpec.Bones; i++)
            {
                Transform? bone = RigBone.Named(root, TentacleRig.BonePrefix + i.ToString("00"));
                float radius = TentacleSpec.Radius((i + 0.5f) / TentacleSpec.Bones) + Margin;
                if (bone != null)
                {
                    Vector3 middle = root.TransformPoint(0f, 0f, (i + 0.5f) * TentacleSpec.Segment);
                    Capsule(bone, middle, root.rotation, radius, TentacleSpec.Segment + 2f * radius, 2, layer);
                }
            }
        }

        private static void Sphere(Transform bone, Vector3 at, float radius, int layer)
        {
            var sphere = Part(bone, at, bone.rotation, layer).AddComponent<SphereCollider>();
            sphere.radius = radius;
        }

        private static void Capsule(Transform bone, Vector3 at, Quaternion turn, float radius, float height, int axis, int layer)
        {
            var capsule = Part(bone, at, turn, layer).AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.direction = axis;
        }

        // A child of the bone at a world place, unscaled, so its collider is sized in metres whatever the bone's scale.
        private static GameObject Part(Transform bone, Vector3 at, Quaternion turn, int layer)
        {
            var part = new GameObject("hitbox") { layer = layer };
            part.transform.SetParent(bone, false);
            part.transform.SetPositionAndRotation(at, turn);
            Vector3 scale = bone.lossyScale;
            part.transform.localScale = new Vector3(1f / Mathf.Max(scale.x, 1e-4f), 1f / Mathf.Max(scale.y, 1e-4f), 1f / Mathf.Max(scale.z, 1e-4f));
            return part;
        }
    }
}
