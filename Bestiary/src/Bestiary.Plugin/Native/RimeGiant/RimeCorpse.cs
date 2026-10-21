// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The giant's corpse: a copy of the forest troll's ragdoll at the giant's size (the game spawns a ragdoll at its
    /// prefab's own size, on every machine alike) with the giant's star looks. Its frost comes from the colour the
    /// game hands every ragdoll from the dying creature (<see cref="RimeGiantColorPatch"/>).
    /// </summary>
    public static class RimeCorpse
    {
        public static GameObject? Build(Humanoid troll)
        {
            GameObject? ragdoll = null;
            foreach (EffectList.EffectData effect in troll.m_deathEffects.m_effectPrefabs)
            {
                if (effect.m_prefab != null && effect.m_prefab.GetComponent<Ragdoll>() != null)
                {
                    ragdoll = effect.m_prefab;
                }
            }
            if (ragdoll == null)
            {
                return null;
            }
            GameObject corpse = PrefabBench.Copy(ragdoll, RimeGiantPrefabs.Corpse);
            corpse.transform.localScale = ragdoll.transform.localScale * RimeGiantCreature.Size;
            RimeLook.StarLooks(corpse);
            return corpse;
        }
    }
}
