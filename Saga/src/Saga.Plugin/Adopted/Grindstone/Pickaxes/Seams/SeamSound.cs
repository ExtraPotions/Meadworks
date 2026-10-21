// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/SeamSound.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The small sound of a seam opening, for the miner only: the game's crystal placing clink (sfx_build_hammer_crystal;
    /// the coin clink when a game update removed it), quieter and higher than the game plays it, a little higher again
    /// for each link of a chain. A local copy of the sound prefab, instantiated with network views disabled, the way the
    /// game makes its build ghost, so no ZDO is made and nobody else hears it. The game's sound player (ZSFX) plays it in
    /// its Start, so the copy is tuned right after it is made; it is removed after a few seconds whatever its own timer does.
    /// </summary>
    internal static class SeamSound
    {
        private const float Life = 5f;
        private const float Volume = 0.45f;
        private const float Pitch = 1.2f;
        private const float PitchPerLink = 0.06f;
        private const int RisingLinks = 6;

        private static readonly string[] Names = { "sfx_build_hammer_crystal", "sfx_coins_placed" };

        private static GameObject prefab;

        /// <summary>Plays the opening sound at <paramref name="position"/>; <paramref name="links"/> is the chain so far.</summary>
        public static void Play(Vector3 position, int links)
        {
            GameObject sound = Prefab();
            if (sound == null)
                return;
            GameObject copy = LocalCopy(sound, position);
            float pitch = Pitch + PitchPerLink * Mathf.Clamp(links, 0, RisingLinks);
            foreach (ZSFX sfx in copy.GetComponentsInChildren<ZSFX>(true))
            {
                sfx.m_minVol *= Volume;
                sfx.m_maxVol *= Volume;
                sfx.m_minPitch *= pitch;
                sfx.m_maxPitch *= pitch;
            }
            Object.Destroy(copy, Life);
        }

        private static GameObject LocalCopy(GameObject sound, Vector3 position)
        {
            bool was = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            try
            {
                return Object.Instantiate(sound, position, Quaternion.identity);
            }
            finally
            {
                ZNetView.m_forceDisableInit = was;
            }
        }

        private static GameObject Prefab()
        {
            if (prefab != null || ZNetScene.instance == null)
                return prefab;
            foreach (string name in Names)
            {
                prefab = ZNetScene.instance.GetPrefab(name);
                if (prefab != null)
                    break;
            }
            return prefab;
        }
    }
}
