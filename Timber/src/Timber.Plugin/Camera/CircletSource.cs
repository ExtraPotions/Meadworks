#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CircletSource.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
using UnityEngine;

namespace Timber.Camera
{
    /// <summary>
    /// Finds the light the local player wears on the head: any light under the head bone, which holds the helmet's
    /// attach point (<c>VisEquipment.m_helmet</c>), so the Dvergr circlet in the helmet slot and a circlet another mod
    /// hangs on the head from an extra slot are both found, while torches in hand or on the back are not. A light counts
    /// while its object is active and it is on, or the game's light fading (<c>LightLod</c>) drives it; of several the
    /// strongest wins. Looked up twice a second; the found light is kept while it stays lit.
    /// </summary>
    public static class CircletSource
    {
        private const float LookupSeconds = 0.5f;
        private const string HeadBone = "Head";

        private static Light found;
        private static float nextLookup;

        public static void Reset() { found = null; nextLookup = 0f; }

        public static Light Find(Player player)
        {
            if (Time.time < nextLookup)
                return found != null && Lit(found) ? found : null;
            nextLookup = Time.time + LookupSeconds;
            Transform head = Head(player);
            found = head != null ? Strongest(head) : null;
            return found;
        }

        public static bool Lit(Light light)
        {
            return light.gameObject.activeInHierarchy && (light.enabled || light.GetComponent<LightLod>() != null);
        }

        /// <summary>The bone named Head above the helmet's attach point, or the attach point's parent.</summary>
        private static Transform Head(Player player)
        {
            Transform helmet = player != null && player.m_visEquipment != null ? player.m_visEquipment.m_helmet : null;
            if (helmet == null)
                return null;
            for (Transform bone = helmet; bone != null && bone != player.transform; bone = bone.parent)
            {
                if (bone.name == HeadBone)
                    return bone;
            }
            return helmet.parent;
        }

        private static Light Strongest(Transform head)
        {
            Light best = null;
            foreach (Light light in head.GetComponentsInChildren<Light>())
            {
                if (Lit(light) && (best == null || Strength(light) > Strength(best)))
                    best = light;
            }
            return best;
        }

        private static float Strength(Light light) => light.intensity * BaseRange(light);

        /// <summary>The light's full range: the game's light fading shrinks the range itself while it fades.</summary>
        public static float BaseRange(Light light)
        {
            LightLod lod = light.GetComponent<LightLod>();
            return lod != null && lod.m_baseRange > 0f ? lod.m_baseRange : light.range;
        }
    }
}
