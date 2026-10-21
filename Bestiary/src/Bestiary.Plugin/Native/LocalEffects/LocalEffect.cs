// Portions Copyright (C) 2026 Gerald Glasgow. ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: private Bestiary namespace.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.LocalEffects
{
    /// <summary>
    /// Copies of the game's effect prefabs (the ones in ZNetScene's prefab list) that exist on this machine only: seen
    /// and heard, never networked, never a damage source. Each machine draws its own copy from state it already has, so
    /// an effect costs no network traffic. A density from 0 to 1 thins the particles and dims the lights; 0 spawns
    /// nothing, so a mod can let each player turn its effects down without changing a point of damage. Sounds are
    /// never thinned: density governs what is seen, not what is heard. A dedicated server sees and hears nothing, so
    /// there every call makes nothing. Spent one-shot bursts are kept for the next burst of their kind where that is
    /// safe (<see cref="BurstPool"/>).
    /// </summary>
    public static class LocalEffect
    {
        /// <summary>The authored radius of a typical game ground effect; one-shot flashes scale from this to match.</summary>
        private const float BaselineRadius = 4f;

        /// <summary>The longest a one-shot sound copy may live, should its own timer be missing.</summary>
        private const float SoundLife = 10f;

        /// <summary>
        /// The effect under a parent (so it dies with it), stripped and thinned; null when the density is 0 or the
        /// prefab is missing. <paramref name="endless"/> removes its own timer, for an effect that lasts as long as
        /// the parent.
        /// </summary>
        public static GameObject? Attach(GameObject? prefab, Transform parent, Vector3 position, bool endless, float density = 1f)
        {
            if (prefab == null || density <= 0f || Headless())
            {
                return null;
            }
            GameObject clone = CloneParts.Instantiate(prefab, position);
            clone.transform.SetParent(parent, worldPositionStays: true);
            CloneParts.Strip(clone, endless);
            CloneParts.Thin(clone, density);
            return clone;
        }

        /// <summary>A free-standing one-shot burst, scaled to a radius, that cleans itself up.</summary>
        public static void Flash(GameObject? prefab, Vector3 position, float radius, float density = 1f) =>
            OneShot(prefab, position, radius, Sizing.Radius, 1f, density);

        /// <summary>
        /// A one-shot burst like <see cref="Flash"/>, drawn whole at <paramref name="scale"/> times its radius-matched
        /// size. Most game effects scale each particle system by its own transform alone ("Local" scaling), so scaling
        /// the root - all Flash does - resizes only the root system and leaves the child systems at full size; here
        /// every system follows the root, so the whole burst takes the size.
        /// </summary>
        public static void FlashWhole(GameObject? prefab, Vector3 position, float radius, float scale, float density = 1f) =>
            OneShot(prefab, position, radius, Sizing.Whole, scale, density);

        /// <summary>
        /// A one-shot burst drawn exactly as <see cref="Flash"/> draws it, then resized part by part to
        /// <paramref name="scale"/> times that: every particle system's size, speed, forces and emitter shape, the
        /// lights' reach and the distances between the parts. Unlike <see cref="FlashWhole"/> it leaves each system's
        /// scaling mode alone, so every part ends at the same fraction of what the player saw before, in any mode.
        /// </summary>
        public static void FlashScaled(GameObject? prefab, Vector3 position, float radius, float scale, float density = 1f) =>
            OneShot(prefab, position, radius, Sizing.Parts, scale, density);

        /// <summary>
        /// A one-shot sound at a point: the prefab's own sound player plays it as it wakes, and its own timer removes it.
        /// </summary>
        public static void Sound(GameObject? prefab, Vector3 position)
        {
            if (prefab == null || Headless())
            {
                return;
            }
            GameObject clone = CloneParts.Instantiate(prefab, position);
            CloneParts.Strip(clone, endless: false);
            Object.Destroy(clone, SoundLife);
        }

        /// <summary>
        /// Only the sound of an effect prefab, at <paramref name="volume"/> times its own loudness (1 is as the game
        /// plays it): nothing of the copy is drawn, so an effect wanted for its sound costs no particles or lights. Its
        /// own timer removes it, or <see cref="SoundLife"/> should that be missing. A volume of 0 makes nothing.
        /// </summary>
        public static void SoundOnly(GameObject? prefab, Vector3 position, float volume = 1f)
        {
            if (prefab == null || volume <= 0f || Headless())
            {
                return;
            }
            GameObject clone = CloneParts.Instantiate(prefab, position);
            CloneParts.Strip(clone, endless: false);
            CloneParts.Unseen(clone);
            CloneParts.Quieten(clone, volume);
            Object.Destroy(clone, SoundLife);
        }

        /// <summary>
        /// A burst: a kept one of the same kind shown again, or a fresh copy made, sized and timed. The sizes and the
        /// density are rounded first (<see cref="BurstSizes"/>), for the key and the drawing alike, so a kept copy and
        /// a fresh one of a kind look the same.
        /// </summary>
        private static void OneShot(GameObject? prefab, Vector3 position, float radius, Sizing sizing, float scale, float density)
        {
            if (prefab == null || density <= 0f || Headless())
            {
                return;
            }
            radius = BurstSizes.Size(radius);
            scale = BurstSizes.Size(scale);
            density = BurstSizes.Density(density);
            BurstKey key = new BurstKey(prefab, sizing, radius, scale, density);
            if (BurstPool.Reuse(key, position))
            {
                return;
            }
            GameObject clone = CloneParts.Instantiate(prefab, position);
            CloneParts.Strip(clone, endless: false);
            CloneParts.Thin(clone, density);
            clone.transform.localScale *= Mathf.Max(radius, 0.01f) / BaselineRadius;
            Size(clone, sizing, scale);
            BurstPool.Release(prefab, clone, key, Mathf.Max(radius, 3f));
        }

        /// <summary>
        /// <see cref="FlashWhole"/>: every system follows the root, then the root takes the scale.
        /// <see cref="FlashScaled"/>: every part's own numbers resized.
        /// </summary>
        private static void Size(GameObject clone, Sizing sizing, float scale)
        {
            if (sizing == Sizing.Whole)
            {
                CloneParts.FollowRoot(clone);
                clone.transform.localScale *= scale;
            }
            else if (sizing == Sizing.Parts)
            {
                ScaleParts.Apply(clone, scale);
            }
        }

        /// <summary>A dedicated server: nobody there sees or hears an effect.</summary>
        private static bool Headless() => ZNet.instance != null && ZNet.instance.IsDedicated();
    }
}
