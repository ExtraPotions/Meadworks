// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Adopted.Visuals;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The Bloated explosion itself, fired the instant the owner's fuse ends (the delay is spent by the corpse rider, see
    /// <see cref="BloatedCorpse"/>). It is driven from a routed broadcast (see <see cref="Runtime.EliteRpc"/>): every
    /// client draws the burst and plays the bang at the owner's corpse position, but only the owner's copy is
    /// <c>damaging</c> and deals the blunt damage in a radius, scaled by star count as (1 + stars). It has no attacker,
    /// so it feeds nothing. A dedicated server draws nothing, and deals the damage only when it was the owner.
    /// </summary>
    public static class BloatedBlast
    {
        /// <summary>
        /// The burst is drawn, every part of it, at 70% of the size its radius alone would give it: at full size it looked
        /// too big for the blast. Visual only - the damage still reaches the whole <see cref="BlastSpec.Radius"/>.
        /// </summary>
        private const float DrawScale = 0.7f;

        public static void Detonate(Vector3 pos, BlastSpec blast, bool damaging)
        {
            if (!Machine.Headless)
            {
                Draw(pos, blast); // every machine with a screen draws and hears the blast
            }
            if (!damaging) // only the owner's blast decides damage; remote copies are visual only
            {
                return;
            }
            List<Character> found = new List<Character>();
            Character.GetCharactersInRange(pos, blast.Radius, found);
            float damage = blast.Damage * (1 + blast.Stars);
            foreach (Character character in found)
            {
                if (character != null && !character.IsDead())
                {
                    character.Damage(BuildHit(pos, damage));
                }
            }
        }

        private static void Draw(Vector3 pos, BlastSpec blast)
        {
            GameObject? prefab = EffectResolver.Resolve(blast.Effect, EffectResolver.Blast, "Bloated blast effect");
            CosmeticClone.FlashWhole(prefab, pos, blast.Radius, DrawScale);
            GameObject? sound = EffectResolver.ResolveSound(blast.Sound, EffectResolver.BlastSound, "Bloated blast sound");
            CosmeticClone.Sound(sound, pos);
        }

        private static HitData BuildHit(Vector3 pos, float damage)
        {
            HitData hit = new HitData();
            hit.m_damage.m_blunt = damage;
            hit.m_point = pos;
            hit.m_pushForce = 40f;
            hit.m_hitType = HitData.HitType.EnemyHit;
            return hit;
        }
    }
}
