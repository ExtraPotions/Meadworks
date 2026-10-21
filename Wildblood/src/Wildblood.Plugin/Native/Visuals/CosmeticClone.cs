// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.AdoptedLibs.LocalEffects;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Visuals
{
    /// <summary>
    /// Clones a resolved vanilla effect prefab as a purely local, cosmetic object: never networked, never a damage
    /// source (the ExtraPotions.Wildblood.AdoptedLibs.LocalEffects library's <see cref="LocalEffect"/>). Density from the per-player setting thins the
    /// particles; zero density spawns nothing - which is what lets a player turn effects down to nothing without changing
    /// a single point of damage. A sound clone is the same stripped, local copy, and plays whatever the density. A
    /// dedicated server makes none of them (<see cref="Machine.Headless"/>): no one there could see or hear them.
    /// </summary>
    public static class CosmeticClone
    {
        /// <summary>Spawns the effect under a parent (so it dies with it), stripped and thinned; null when off or absent.</summary>
        public static GameObject? Spawn(GameObject? prefab, Transform parent, Vector3 position, bool endless)
        {
            GameObject? clone = Machine.Headless ? null : LocalEffect.Attach(prefab, parent, position, endless, Density());
            if (endless && parent != null) parent.GetComponent<Runtime.EliteController>()?.OwnVisual(clone);
            return clone;
        }

        /// <summary>A free-standing one-shot effect, scaled to a radius, that cleans itself up; used for brief bursts.</summary>
        public static void Flash(GameObject? prefab, Vector3 position, float radius)
        {
            if (!Machine.Headless)
            {
                LocalEffect.Flash(prefab, position, radius, Density());
            }
        }

        /// <summary>A one-shot burst like <see cref="Flash"/>, drawn whole at <paramref name="scale"/> times its size.</summary>
        public static void FlashWhole(GameObject? prefab, Vector3 position, float radius, float scale)
        {
            if (!Machine.Headless)
            {
                LocalEffect.FlashWhole(prefab, position, radius, scale, Density());
            }
        }

        /// <summary>A one-shot sound at a point; not thinned by the effect density setting.</summary>
        public static void Sound(GameObject? prefab, Vector3 position)
        {
            if (!Machine.Headless)
            {
                LocalEffect.Sound(prefab, position);
            }
        }

        /// <summary>An effect's own sound alone at <paramref name="volume"/> of its loudness, nothing of it drawn; not
        /// thinned by the effect density setting either.</summary>
        public static void SoundOnly(GameObject? prefab, Vector3 position, float volume)
        {
            if (!Machine.Headless)
            {
                LocalEffect.SoundOnly(prefab, position, volume);
            }
        }

        private static float Density() => Mathf.Clamp01(Configuration.EffectDensity.Value);
    }
}
