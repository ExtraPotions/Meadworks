// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// What a dead mimic leaves: a copy of the draugr's ragdoll (its ZNetView, fade-out and loot hand-off; the skeleton
    /// has none, it bursts into bones) carrying the mimic's body instead of the draugr's. The body plays the death
    /// animation - the lid flops open, the tongue lolls, the eyes go out - then the game's ragdoll timer removes it and spills the loot the creature's drop list saved into it,
    /// the same path the game and loot mods use for every creature.
    /// </summary>
    public static class MimicCorpse
    {
        public static GameObject Build(GameObject ragdoll, GameObject visual)
        {
            GameObject corpse = PrefabBench.Copy(ragdoll, MimicPrefabs.Corpse);
            for (int i = corpse.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(corpse.transform.GetChild(i).gameObject);   // the draugr's bones and mesh
            }
            visual.transform.SetParent(corpse.transform, false);
            LevelEffects looks = corpse.GetComponent<LevelEffects>();
            if (looks != null)
            {
                Object.DestroyImmediate(looks); // it colours the draugr's mesh, which is gone
            }
            var ragdollPart = corpse.GetComponent<Ragdoll>();
            ragdollPart.m_mainModel = null;
            ragdollPart.m_ttl = 6f;
            ragdollPart.m_float = false;
            corpse.AddComponent<MimicDeathPose>();
            return corpse;
        }
    }

    /// <summary>Puts the corpse's animator straight into the death animation, on every client that shows it.</summary>
    public class MimicDeathPose : MonoBehaviour
    {
        private void Start()
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.SetBool("sleeping", false);
                animator.SetBool("dead", true);
            }
        }
    }
}
