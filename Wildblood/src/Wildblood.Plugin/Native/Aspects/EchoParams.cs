// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The animator values the game itself keeps in step across machines - the ones a creature's <see cref="ZSyncAnimation"/>
    /// lists: walking and turning speeds, on the ground, flying, swimming and the like - read off an Echoing boss into one
    /// row of floats, and written back onto its echo through the echo's own sync, so every machine sees the echo walk,
    /// turn and fly as the boss did. Boss and echo are the same prefab, so their lists match entry for entry.
    /// </summary>
    internal static class EchoParams
    {
        public static int Count(ZSyncAnimation anim) =>
            anim.m_boolHashes.Length + anim.m_floatHashes.Length + anim.m_intHashes.Length;

        public static void Read(ZSyncAnimation anim, float[] into)
        {
            Animator animator = anim.m_animator;
            int i = 0;
            foreach (int hash in anim.m_boolHashes)
            {
                into[i++] = animator.GetBool(hash) ? 1f : 0f;
            }
            foreach (int hash in anim.m_floatHashes)
            {
                into[i++] = animator.GetFloat(hash);
            }
            foreach (int hash in anim.m_intHashes)
            {
                into[i++] = animator.GetInteger(hash);
            }
        }

        /// <summary>Owner only: the sync writes each changed value to the echo's ZDO for the other machines.</summary>
        public static void Write(ZSyncAnimation anim, float[] values)
        {
            if (values.Length != Count(anim))
            {
                return; // never a mismatch between two copies of one prefab; a guard, not a case
            }
            int i = 0;
            foreach (int hash in anim.m_boolHashes)
            {
                anim.SetBool(hash, values[i++] > 0.5f);
            }
            foreach (int hash in anim.m_floatHashes)
            {
                anim.SetFloat(hash, values[i++]);
            }
            foreach (int hash in anim.m_intHashes)
            {
                anim.SetInt(hash, Mathf.RoundToInt(values[i++]));
            }
        }
    }
}
