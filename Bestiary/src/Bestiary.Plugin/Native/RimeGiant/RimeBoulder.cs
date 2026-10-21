// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The ice boulder the giant hurls: a copy of the forest troll's thrown rock wearing the bundle's boulder instead of
    /// the rock, bursting where it lands so everyone within <see cref="Burst"/> metres takes the throw's frost and
    /// half as much blunt (the game's frost chills and slows), with the game's ice shattering on top of the rock's own hit.
    /// </summary>
    public static class RimeBoulder
    {
        public const float Burst = 3.5f;

        public static GameObject Build(GameObject rock, GameObject? look, Material skin, ZNetScene scene)
        {
            GameObject boulder = PrefabBench.Copy(rock, RimeGiantPrefabs.Boulder);
            var projectile = boulder.GetComponent<Projectile>();
            projectile.m_aoe = Burst;
            projectile.m_hitEffects.m_effectPrefabs = WithIce(projectile.m_hitEffects.m_effectPrefabs, scene);
            if (look != null && projectile.m_visual != null)
            {
                Reskin(projectile.m_visual.transform, look, skin);
            }
            return boulder;
        }

        /// <summary>
        /// The rock's renderers off, the boulder in their place at the rock mesh's centre (about 25 cm above the
        /// projectile's root), at its own size in metres, in the troll's lighting.
        /// </summary>
        private static void Reskin(Transform visual, GameObject look, Material skin)
        {
            MeshFilter? rock = visual.GetComponentInChildren<MeshFilter>(true);
            Vector3 centre = rock != null && rock.sharedMesh != null
                ? visual.InverseTransformPoint(rock.transform.TransformPoint(rock.sharedMesh.bounds.center))
                : Vector3.zero;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
            GameObject ice = Object.Instantiate(look, visual, false);
            ice.name = "ecr_rime_boulder";
            ice.transform.localPosition = centre;
            ice.transform.localScale = Vector3.one * (1f / Mathf.Max(visual.lossyScale.x, 0.0001f));
            foreach (Renderer renderer in ice.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = GameMaterials.Dress(skin, renderer.sharedMaterial);
            }
        }

        private static EffectList.EffectData[] WithIce(EffectList.EffectData[] rock, ZNetScene scene)
        {
            var effects = new List<EffectList.EffectData>(rock);
            foreach (string name in new[] { "vfx_ice_destroyed", "sfx_ice_destroyed" })
            {
                GameObject? prefab = scene.GetPrefab(name);
                if (prefab != null)
                {
                    effects.Add(new EffectList.EffectData { m_prefab = prefab });
                }
            }
            return effects.ToArray();
        }
    }
}
