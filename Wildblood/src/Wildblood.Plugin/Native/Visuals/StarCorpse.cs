// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Visuals
{
    /// <summary>
    /// A starred creature's corpse keeps its star look. The game dresses a ragdoll from the dying creature's level, which
    /// is 1 for every elite. Its tint is corrected where the game reads it (<see cref="Patches.StarLookColorPatch"/>) and
    /// travels on the ragdoll's record by the game's own route. The rest of the look is ours: on the creature's owner,
    /// as the game makes the ragdoll, the star count is written on the ragdoll's record in the same frame, and every
    /// other machine dresses its copy from it as the copy wakes.
    /// </summary>
    internal static class StarCorpse
    {
        /// <summary>Owner side, as the creature makes its ragdoll.</summary>
        public static void Made(Character creature, Ragdoll ragdoll)
        {
            int stars = StarLook.StarsOf(creature);
            ZNetView nview = ragdoll.GetComponent<ZNetView>();
            if (stars <= 0 || nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return;
            }
            TraitKeys.MigrateAuxiliary(nview.GetZDO());
            nview.GetZDO().Set(TraitKeys.CorpseStars, stars);
            Show(ragdoll, stars);
        }

        /// <summary>
        /// Every machine, as a ragdoll wakes. The owner's own copy wakes before the stars are written, reads none, and is
        /// dressed in <see cref="Made"/> instead.
        /// </summary>
        public static void Wake(Ragdoll ragdoll)
        {
            ZNetView nview = ragdoll.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid()) TraitKeys.MigrateAuxiliary(nview.GetZDO());
            int stars = nview != null && nview.IsValid() ? TraitKeys.Int(nview.GetZDO(), TraitKeys.CorpseStars) : 0;
            if (stars > 0)
            {
                Show(ragdoll, stars);
            }
        }

        private static void Show(Ragdoll ragdoll, int stars)
        {
            foreach (LevelEffects effects in ragdoll.GetComponentsInChildren<LevelEffects>())
            {
                StarLook.Show(effects, stars);
            }
        }
    }
}
