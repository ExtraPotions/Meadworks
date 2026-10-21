// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Runtime.Native.LocalEffects;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The game's own ice and frost effects the giant uses, found by name (the first of each list the game has) and drawn
    /// locally on each machine through <see cref="LocalEffect"/>: a plate shattering, a plate growing back, a step
    /// of the avalanche. Each machine draws its own from state it already has, so none of this crosses the network.
    /// A name the game lacks is logged once and that effect is left out.
    /// </summary>
    internal static class RimeEffects
    {
        private static readonly string[] Shatter = { "vfx_ice_destroyed", "vfx_ice_hit" };
        private static readonly string[] ShatterSound = { "sfx_ice_destroyed", "sfx_ice_hit" };
        private static readonly string[] Frost = { "vfx_Frost", "vfx_ice_hit" };
        private static readonly string[] FrostSound = { "sfx_Frost_Start", "sfx_ice_hit" };
        private static readonly string[] Snow = { "vfx_BombBlob_explode_frost", "vfx_troll_groundslam" };
        private static readonly string[] SnowSound = { "sfx_ice_destroyed", "sfx_troll_rock_destroyed" };
        private static readonly Dictionary<string, GameObject?> found = new Dictionary<string, GameObject?>();

        /// <summary>A plate breaking off.</summary>
        public static void Break(Vector3 at)
        {
            LocalEffect.Flash(Find(Shatter), at, 2f);
            LocalEffect.Sound(Find(ShatterSound), at);
        }

        /// <summary>A plate growing back.</summary>
        public static void Grow(Vector3 at)
        {
            LocalEffect.Flash(Find(Frost), at, 1.5f);
            LocalEffect.Sound(Find(FrostSound), at);
        }

        /// <summary>One step of the avalanche; the sound only on every third, so the wave rumbles rather than rattles.</summary>
        public static void Wave(Vector3 at, int step)
        {
            LocalEffect.FlashWhole(Find(Snow), at, 3f, 1f);
            if (step % 3 == 0)
            {
                LocalEffect.Sound(Find(SnowSound), at);
            }
        }

        /// <summary>The first of the names the game has, as a network prefab; the lookup is kept, the miss logged once.</summary>
        public static GameObject? Find(string[] names)
        {
            string key = names[0];
            if (found.TryGetValue(key, out GameObject? prefab) && prefab != null)
            {
                return prefab;
            }
            prefab = null;
            foreach (string name in names)
            {
                prefab = prefab ?? ZNetScene.instance?.GetPrefab(name);
            }
            if (prefab == null && !found.ContainsKey(key))
            {
                Log.Warn($"Rime giant: the game has none of {string.Join(", ", names)}; that effect is left out.");
            }
            found[key] = prefab;
            return prefab;
        }
    }
}
