// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// A Colossal boss's corpse keeps the boss's size. The game spawns a boss's ragdoll at its prefab's own size, so a boss
    /// grown 40% would shrink back to normal the moment it fell. The ragdoll is made on the boss's owner; there it is
    /// grown by the ratio of the boss's size to its prefab's, and that ratio is written on the ragdoll's own network
    /// record in the same frame, so every other machine grows its copy from the record as the ragdoll wakes there. The
    /// live size is measured rather than recomputed from the rules, so a rule reload since the boss appeared cannot make
    /// the two differ. Eikthyr, the Elder, Yagluth and the Fader leave ragdolls; Bonemass, Moder and the Queen leave only
    /// a death effect, which keeps its own size.
    /// </summary>
    internal static class ColossalCorpse
    {
        /// <summary>On the ragdoll's ZDO: the size its boss had grown to, as a multiple of the prefab's.</summary>
        private const string ScaleKey = Traits.TraitKeys.CorpseScale;

        /// <summary>Owner side, as the boss makes its ragdoll: grow it to the boss's size and record that size.</summary>
        public static void Grow(Character boss, Ragdoll ragdoll)
        {
            EliteController controller = boss.GetComponent<EliteController>();
            ZNetView nview = ragdoll.GetComponent<ZNetView>();
            if (controller == null || !controller.Ready || !controller.Traits.HasAspect(Aspect.Colossal)
                || nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return;
            }
            TraitKeys.MigrateAuxiliary(nview.GetZDO());
            float size = SizeOf(boss.gameObject);
            if (!Mathf.Approximately(size, 1f) && Mathf.Approximately(SizeOf(ragdoll.gameObject), 1f)) // not sized already
            {
                nview.GetZDO().Set(ScaleKey, size);
                ragdoll.transform.localScale *= size;
            }
        }

        /// <summary>
        /// Every machine, as a ragdoll wakes: one that arrives with a size on its record grows to it. The owner's own copy
        /// wakes before the size is written, reads nothing, and is grown in <see cref="Grow"/> instead.
        /// </summary>
        public static void Wake(Ragdoll ragdoll)
        {
            ZNetView nview = ragdoll.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid()) TraitKeys.MigrateAuxiliary(nview.GetZDO());
            float size = nview != null && nview.IsValid() ? TraitKeys.Float(nview.GetZDO(), ScaleKey, 1f) : 1f;
            if (!Mathf.Approximately(size, 1f))
            {
                ragdoll.transform.localScale *= size;
            }
        }

        // An object's live size as a multiple of its prefab's: for the boss, stars and Colossal together, however they
        // were applied; for a ragdoll, anything other than 1 means a death effect already sized it (a modded boss's may).
        private static float SizeOf(GameObject instance)
        {
            ZNetScene scene = ZNetScene.instance;
            GameObject? prefab = scene != null ? scene.GetPrefab(Utils.GetPrefabName(instance)) : null;
            float authored = prefab != null ? prefab.transform.localScale.x : 1f;
            return authored > 0f ? instance.transform.localScale.x / authored : 1f;
        }
    }
}
