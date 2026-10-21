// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Visuals
{
    /// <summary>
    /// Turns a configured vanilla-prefab name into an actual effect prefab through ZNetScene, so a server retunes a
    /// look by editing a string. A name that resolves is used as-is; a name that does not falls back to the nearest
    /// effect of the same kind - a visual effect (named <c>fx_</c> or <c>vfx_</c>) that renders, or a sound effect
    /// (named <c>sfx_</c>) that plays - whose name matches the role's keywords, the first keyword first, logged once, so
    /// a mistyped or version-shifted name self-heals into a visible effect rather than an invisible hazard. The name
    /// prefix keeps a keyword from ever matching a creature or an item. Nothing here throws.
    /// </summary>
    public static class EffectResolver
    {
        public static readonly string[] Cloud = { "poison", "blob", "ooze", "puke", "gas", "spore", "smoke" };
        public static readonly string[] Blast = { "explos", "blast", "fire", "bomb", "death", "burst", "flame" };
        public static readonly string[] BlastSound = { "explo", "bomb", "blast" };
        public static readonly string[] Warning = { "smoke", "fire", "burn", "charge", "glow", "spark" };
        // Warding and Devouring get no prefab field in the spec's power table, so they resolve by keyword only. The
        // reflect tell is only heard (see CreatureRpc), so its first keyword pins one effect with a sound of its own: the
        // Staff of Protection's bubble taking a blow. Plain "shield" alone left it to whichever the game lists first.
        public static readonly string[] Reflect = { "staffshield_hit", "shield", "spark", "hit", "block", "staff" };
        // The instant-kill tell: a death burst's own sound, so a distant player hears a creature cease. Only heard now (see
        // CreatureRpc); the first keyword pins the one it has always resolved to, the game's fx_aspect_death.
        public static readonly string[] Devour = { "aspect_death", "death", "gore", "corpse", "destr", "blood", "hit" };
        // Thieving gets no prefab field in the spec's power table either; the steal tell resolves by keyword only.
        public static readonly string[] Steal = { "steal", "pickpocket", "pocket", "grab", "loot", "coin" };
        // Boss aspects: Summoner's arrival tell and a Phantom copy's vanishing puff, keyword-only like the rest.
        public static readonly string[] Summon = { "spawn", "summon", "portal", "smoke", "puff" };
        public static readonly string[] Phantom = { "ghost", "wisp", "puff", "smoke", "vanish", "poof" };

        /// <summary>Sounds are cached apart from visuals, so one name asked for as both never returns the wrong kind.</summary>
        private const string SoundKey = "sound:";

        private static readonly Dictionary<string, GameObject?> Cache = new Dictionary<string, GameObject?>();

        /// <summary>
        /// The prefab for a configured name, resolving and logging at most once; null only when nothing fits. The
        /// <paramref name="setting"/> is the rule-file field the name came from (e.g. "Miasmic cloud effect"); it is named
        /// in the log when a name fails to resolve, so a silently substituted effect can be traced to the setting to fix.
        /// </summary>
        public static GameObject? Resolve(string name, string[] keywords, string setting = "") =>
            Lookup(name, keywords, setting, sound: false);

        /// <summary>The sound prefab for a configured name, the same way: a prefab that plays a sound, or the nearest one.</summary>
        public static GameObject? ResolveSound(string name, string[] keywords, string setting = "") =>
            Lookup(name, keywords, setting, sound: true);

        /// <summary>Resolves a role name (as it travels over the effect bus) to its keyword-only tell; null if unknown.</summary>
        public static GameObject? ForRole(string role)
        {
            if (role == "reflect" || role == "devour")
            {
                return ByKeyword(role, role == "reflect" ? Reflect : Devour);
            }
            if (role == "summon" || role == "phantom")
            {
                return ByKeyword(role, role == "summon" ? Summon : Phantom);
            }
            return role == "steal" ? ByKeyword(role, Steal) : null;
        }

        /// <summary>Resolves an effect purely by keyword for tells the spec gives no config field; logged once per role.</summary>
        public static GameObject? ByKeyword(string role, string[] keywords)
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null)
            {
                return null;
            }
            if (Cache.TryGetValue(role, out GameObject? cached))
            {
                return cached;
            }
            GameObject? found = Nearest(scene, keywords, sound: false);
            if (found == null)
            {
                Log.Warn($"no vanilla effect matched the {role} tell; it will not be shown");
            }
            else if (Log.Diagnostics)
            {
                Log.Diag($"the {role} tell is {found.name}");
            }
            Cache[role] = found;
            return found;
        }

        /// <summary>
        /// Every prefab the scene knows. The game ships its effects in the networked list - each carries a network view,
        /// which the cosmetic clone disables before it wakes, then strips - and leaves the non-networked list empty,
        /// though another mod may fill it.
        /// </summary>
        public static IEnumerable<GameObject> Prefabs(ZNetScene scene)
        {
            foreach (GameObject prefab in scene.m_prefabs)
            {
                yield return prefab;
            }
            foreach (GameObject prefab in scene.m_nonNetViewPrefabs)
            {
                yield return prefab;
            }
        }

        /// <summary>True for a prefab named as an effect - visual or sound - rather than a creature, item or piece.</summary>
        public static bool IsEffectName(GameObject? prefab) => prefab != null && (Named(prefab, false) || Named(prefab, true));

        private static GameObject? Lookup(string name, string[] keywords, string setting, bool sound)
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            string key = sound ? SoundKey + name : name;
            if (Cache.TryGetValue(key, out GameObject? cached))
            {
                return cached;
            }
            GameObject? found = Find(scene, name, keywords, setting, sound);
            Cache[key] = found;
            return found;
        }

        private static GameObject? Find(ZNetScene scene, string name, string[] keywords, string setting, bool sound)
        {
            GameObject? direct = Fits(scene.GetPrefab(name), sound);
            if (direct != null)
            {
                return direct;
            }
            string from = string.IsNullOrEmpty(setting) ? "" : $" (from setting '{setting}')";
            GameObject? nearest = Nearest(scene, keywords, sound);
            Log.Warn(nearest != null
                ? $"effect prefab '{name}'{from} not found; falling back to '{nearest.name}'"
                : $"effect prefab '{name}'{from} not found and no fallback effect exists - it stays "
                    + (sound ? "silent" : "invisible"));
            return nearest;
        }

        private static GameObject? Nearest(ZNetScene scene, string[] keywords, bool sound)
        {
            foreach (string keyword in keywords)
            {
                foreach (GameObject prefab in Prefabs(scene))
                {
                    if (prefab != null && Named(prefab, sound) && prefab.name.ToLowerInvariant().Contains(keyword)
                        && Fits(prefab, sound) != null)
                    {
                        return prefab;
                    }
                }
            }
            return null;
        }

        private static bool Named(GameObject prefab, bool sound)
        {
            string lower = prefab.name.ToLowerInvariant();
            return sound ? lower.StartsWith("sfx_") : lower.StartsWith("fx_") || lower.StartsWith("vfx_");
        }

        private static GameObject? Fits(GameObject? prefab, bool sound) => sound ? Audible(prefab) : Usable(prefab);

        /// <summary>A prefab is usable as an effect only if it actually renders something.</summary>
        private static GameObject? Usable(GameObject? prefab)
        {
            if (prefab == null)
            {
                return null;
            }
            bool renders = prefab.GetComponentInChildren<ParticleSystem>(true) != null
                || prefab.GetComponentInChildren<Renderer>(true) != null;
            return renders ? prefab : null;
        }

        /// <summary>A prefab is usable as a sound only if the game's own sound player is on it.</summary>
        private static GameObject? Audible(GameObject? prefab) =>
            prefab != null && prefab.GetComponentInChildren<ZSFX>(true) != null ? prefab : null;
    }
}
