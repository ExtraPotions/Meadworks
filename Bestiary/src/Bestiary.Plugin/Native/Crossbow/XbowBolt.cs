// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// A bolt in flight: a copy of the game's bone bolt projectile (arbalest_projectile_bone) wearing the bundle's own
    /// blunted bone bolt (the one in the crossbowman's groove and quiver, grimed like the skeleton) in place of the
    /// game's look. The crossbowman's flies straight, as the skeleton archer's arrow does, so it lands where the game
    /// aimed it: at the target's middle as it stood when the bolt left; its damage comes from the shot
    /// (<see cref="XbowShot"/>). The players' Blunted Bone Bolts fly as the game's bolts do (<see cref="XbowBolts"/>).
    /// Hit, sound and trail are the game's.
    /// </summary>
    public static class XbowBolt
    {
        private const string GameLook = "default";   // the game projectile's own bolt model, flying along +Z
        private const float Length = 0.57f;          // AssetWorkshop assets/ecp_xbow_bolt: nock at its origin, head along +Z (the game bone bolt's length)
        private const float Head = 0.05f;            // where the game projectile's own head is, ahead of its origin

        public static GameObject Build(GameObject gameBolt, string name, bool straight, GameObject look, Material skin)
        {
            GameObject bolt = PrefabBench.Copy(gameBolt, name);
            var projectile = bolt.GetComponent<Projectile>();
            if (projectile != null && straight)
            {
                projectile.m_gravity = 0f;
                projectile.m_ttl = 10f;
            }
            Transform? old = bolt.transform.Find(GameLook);
            Wear(bolt.transform, look, skin, old != null ? old.gameObject.layer : bolt.layer).transform.localPosition = new Vector3(0f, 0f, Head - Length);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
            return bolt;
        }

        /// <summary>The bundle's bolt under `parent`, on `layer`, dressed in the skeleton's material; nock at the parent's origin.</summary>
        public static GameObject Wear(Transform parent, GameObject look, Material skin, int layer)
        {
            GameObject ours = Object.Instantiate(look, parent, false);
            ours.name = "ecp_xbow_bolt_look";
            foreach (Transform part in ours.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = layer;
            }
            XbowKit.Dress(ours, skin);
            return ours;
        }

        /// <summary>The bolt's length, nock to head, for centring it.</summary>
        public static float Size => Length;
    }
}
