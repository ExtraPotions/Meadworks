// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The giant's colour: the forest troll's skin drained of its blue and lightened towards the grey-white of old ice,
    /// through the game's own hue, saturation and value controls on the creature shader. The unstarred look is baked
    /// into the giant's material; each star look is the same frost with its own shift, since the game sets those three
    /// outright when it shows a star look. The corpse gets the same through the ragdoll's own colour hand-off (see
    /// <see cref="RimeGiantColorPatch"/>), so a giant does not turn back into a blue troll when it falls.
    /// </summary>
    public static class RimeLook
    {
        public const float Hue = 0f;
        public const float Saturation = -0.55f;
        public const float Value = 0.25f;

        /// <summary>The frost onto every body material of the copy (the troll's own material stays untouched).</summary>
        public static void Tint(Transform visual)
        {
            foreach (SkinnedMeshRenderer renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].HasProperty("_Saturation"))
                    {
                        materials[i] = Frosted(materials[i]);
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>The star looks, frosted: one star a colder blue, two a paler, brighter white.</summary>
        public static void StarLooks(GameObject copy)
        {
            foreach (LevelEffects effects in copy.GetComponentsInChildren<LevelEffects>(true))
            {
                effects.m_levelSetups = new List<LevelEffects.LevelSetup>
                {
                    Look(effects, 0, hue: -0.06f, saturation: -0.35f, value: 0.2f),
                    Look(effects, 1, hue: 0.04f, saturation: -0.75f, value: 0.4f),
                };
            }
        }

        private static LevelEffects.LevelSetup Look(LevelEffects effects, int index, float hue, float saturation, float value)
        {
            LevelEffects.LevelSetup? troll = index < effects.m_levelSetups.Count ? effects.m_levelSetups[index] : null;
            return new LevelEffects.LevelSetup
            {
                m_scale = troll?.m_scale ?? 1f,
                m_hue = hue,
                m_saturation = saturation,
                m_value = value,
                m_enableObject = troll?.m_enableObject,
            };
        }

        private static Material Frosted(Material troll)
        {
            var material = new Material(troll) { name = troll.name + "_rime" };
            material.SetFloat("_Hue", Hue);
            material.SetFloat("_Saturation", Saturation);
            material.SetFloat("_Value", Value);
            return material;
        }
    }
}
