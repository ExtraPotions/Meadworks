// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// f331465b3ed2551c89decc42ba81565a7b689543
// Death-brood exclusion adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood namespace and retained mutation exclusions.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// Creatures that release other creatures as they die - the Oozer bursting into Blobs, a modded creature built the
    /// same way - decided here and nowhere else. The prefab is read, not a list: a creature among its death effects, its
    /// ragdoll's removal effects or its drops. Such a kind never carries Splintering: every copy would release its own
    /// brood again, so one kill became dozens of creatures (a player's report, 2026-10-13). The verdict is kept per prefab,
    /// and every machine reads the same prefab, so every machine agrees with nothing synced.
    /// </summary>
    public static class DeathBrood
    {
        /// <summary>What a kind that releases creatures never takes, packed like a trait mask: Splintering.</summary>
        public const int Bars = 1 << (int)Mutation.Splintering;

        private static readonly Dictionary<string, bool> Verdicts = new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary><see cref="Bars"/> when the kind releases creatures as it dies, else 0.</summary>
        public static int BarsOf(string prefab) => Releases(prefab) ? Bars : 0;

        /// <summary>True when the prefab's death brings other creatures into the world.</summary>
        public static bool Releases(string prefab)
        {
            if (Verdicts.TryGetValue(prefab, out bool releases))
            {
                return releases;
            }
            GameObject? body = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            if (body == null)
            {
                return false; // no scene yet, or no such prefab: not kept, so it is read once the scene is up
            }
            releases = Read(body);
            Verdicts[prefab] = releases;
            return releases;
        }

        private static bool Read(GameObject body)
        {
            Character character = body.GetComponent<Character>();
            CharacterDrop drops = body.GetComponent<CharacterDrop>();
            return (character != null && InEffects(character.m_deathEffects, true))
                || (drops != null && InDrops(drops.m_drops));
        }

        // A creature among the effects; a ragdoll among them is searched once more, for what it leaves as it is removed.
        private static bool InEffects(EffectList? effects, bool ragdolls)
        {
            foreach (EffectList.EffectData effect in effects?.m_effectPrefabs ?? Array.Empty<EffectList.EffectData>())
            {
                GameObject? made = effect != null && effect.m_enabled ? effect.m_prefab : null;
                if (made == null)
                {
                    continue;
                }
                Ragdoll? ragdoll = ragdolls ? made.GetComponent<Ragdoll>() : null;
                if (made.GetComponent<Character>() != null || (ragdoll != null && InEffects(ragdoll.m_removeEffect, false)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool InDrops(List<CharacterDrop.Drop>? drops)
        {
            foreach (CharacterDrop.Drop drop in drops ?? new List<CharacterDrop.Drop>())
            {
                if (drop?.m_prefab != null && drop.m_prefab.GetComponent<Character>() != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
