// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Runtime.Native.LocalEffects;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The game's own effects the kraken borrows, found by name (the first of each list the game has) and drawn on each
    /// machine through <see cref="LocalEffect"/> from what that machine already knows, so none of it crosses the network:
    /// the Leviathan's surfacing and diving, big splashes, the serpent's roar, a slam on wood, a bite, the tar blob's
    /// spit and splash for the ink. A name the game lacks is logged once and that effect is left out.
    /// </summary>
    internal static class KrakenEffects
    {
        private static readonly string[] Surface = { "fx_leviathan_reaction", "fx_WaterImpact_Big" };
        private static readonly string[] Dive = { "fx_leviathan_leave", "fx_WaterImpact_Big" };
        private static readonly string[] Splash = { "fx_WaterImpact_Big", "fx_float_hitwater" };
        private static readonly string[] Roar = { "sfx_serpent_taunt", "sfx_serpent_alerted" };
        private static readonly string[] Dread = { "sfx_Abomination_alerted", "sfx_bonemaw_serpent_alert", "sfx_serpent_alerted" };
        private static readonly string[] Thud = { "fx_goblinbrute_groundslam", "vfx_troll_groundslam" };
        private static readonly string[] Crash = { "sfx_battering_ram_impact", "sfx_troll_rock_destroyed" };
        private static readonly string[] Swish = { "sfx_Abomination_Attack2_slam_whoosh", "sfx_serpent_attack" };
        private static readonly string[] Snap = { "sfx_bonemaw_serpent_bite", "sfx_serpent_attack_hit" };
        private static readonly string[] Spit = { "sfx_blobtar_attack_spit", "sfx_bonemaw_serpent_spit" };
        private static readonly string[] Blot = { "fx_blobtar_tarball_hit", "fx_BonemawSerpent_Spit_Hit" };
        private static readonly string[] Blob = { "blobtar_projectile_tarball" };
        private static readonly string[] Smack = { "sfx_serpent_attack_hit", "sfx_bear_bite_attack_impact", "sfx_troll_rock_destroyed" };
        private static readonly string[] Chomp = { "sfx_bear_bite_attack_impact", "sfx_serpent_attack_hit" };
        private static readonly string[] Splat = { "sfx_bonemaw_serpent_spit_hit", "sfx_blob_hit" };
        private static readonly Dictionary<string, GameObject?> found = new Dictionary<string, GameObject?>();

        /// <summary>The head or the whole kraken breaking the surface, or catching a ship.</summary>
        public static void Surfacing(Vector3 at) => LocalEffect.Flash(Find(Surface), at, 5f);

        public static void Diving(Vector3 at) => LocalEffect.Flash(Find(Dive), at, 5f);

        /// <summary>A tentacle breaking the surface.</summary>
        public static void Emerging(Vector3 at) => LocalEffect.Flash(Find(Splash), at, 2f);

        public static void Roaring(Vector3 at) => LocalEffect.Sound(Find(Roar), at);

        /// <summary>The warning before the grab: a deep bellow from under the water, and the sea churning where it rises.</summary>
        public static void Warning(Vector3 at)
        {
            LocalEffect.Sound(Find(Dread), at);
            LocalEffect.Sound(Find(Roar), at);
            LocalEffect.Flash(Find(Splash), at, 3f);
        }

        /// <summary>A tentacle starting its downswing.</summary>
        public static void Swinging(Vector3 at) => LocalEffect.Sound(Find(Swish), at);

        /// <summary>A tentacle landing on the ship: a smash (on the ship itself) is bigger.</summary>
        public static void Landing(Vector3 at, bool smash)
        {
            LocalEffect.FlashWhole(Find(Thud), at, smash ? 4f : 2.5f, 1f);
            LocalEffect.Sound(Find(Crash), at);
        }

        public static void Biting(Vector3 at) => LocalEffect.Sound(Find(Snap), at);

        /// <summary>A blow landing on a player: a tentacle's smack (0), the beak's crunch (1) or the ink's splat (2).</summary>
        public static void Struck(Vector3 at, int what)
        {
            LocalEffect.Sound(Find(what == 1 ? Chomp : what == 2 ? Splat : Smack), at);
            if (what == 0)
            {
                LocalEffect.Sound(Find(Crash), at);
            }
        }

        public static void Squirting(Vector3 at) => LocalEffect.Sound(Find(Spit), at);

        /// <summary>The ink splashing on whatever it hits.</summary>
        public static void Blotting(Vector3 at) => LocalEffect.Flash(Find(Blot), at, 3f);

        /// <summary>A drop of ink in flight, under <paramref name="parent"/>, as long as the parent lives.</summary>
        public static GameObject? InkDrop(Transform parent, Vector3 at) => LocalEffect.Attach(Find(Blob), parent, at, endless: true);

        /// <summary>The first of the names the game has, as a network prefab; the lookup is kept, the miss logged once.</summary>
        private static GameObject? Find(string[] names)
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
                Log.Warn($"Kraken: the game has none of {string.Join(", ", names)}; that effect is left out.");
            }
            found[key] = prefab;
            return prefab;
        }
    }
}
