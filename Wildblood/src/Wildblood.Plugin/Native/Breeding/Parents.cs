// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Breeding
{
    /// <summary>
    /// Who the parents are. The game never names a partner: it only counts that one stands close enough when the
    /// pregnancy starts. So at conception the nearest tamed partner in that same range is found and its traits written
    /// onto the pregnant parent's ZDO; at birth the pregnant parent's own traits and that record make the newborn.
    /// Everything here runs on the pregnant parent's owner - the game's breeding runs nowhere else - and the newborn is
    /// instantiated by that same machine, so it owns the newborn it writes.
    /// </summary>
    internal static class Parents
    {
        /// <summary>True when this call to the game's breeding step will give birth: owned, tamed, pregnant and due.</summary>
        public static bool IsBirthNow(Procreation parent)
        {
            ZNetView nview = parent.m_nview;
            return RuleState.Active.Breeding.Enabled && nview != null && nview.IsValid() && nview.IsOwner()
                && parent.m_tameable != null && parent.m_tameable.IsTamed() && parent.IsPregnant() && parent.IsDue();
        }

        /// <summary>At conception: remember the partner beside the parent, or that there was none.</summary>
        public static void Conceive(Procreation parent)
        {
            ZNetView nview = parent.m_nview;
            if (!RuleState.Active.Breeding.Enabled || nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return;
            }
            Character? partner = FindPartner(parent);
            TraitStore.SetSire(nview.GetZDO(), partner != null ? TraitsOf(partner) : null);
        }

        /// <summary>At birth: the newborn of the pregnant parent and the partner remembered at conception.</summary>
        public static Lineage.Newborn Birth(Procreation parent)
        {
            ZDO zdo = parent.m_nview.GetZDO();
            CreatureTraits mother = TraitsOf(parent.m_character);
            CreatureTraits? sire = TraitStore.TakeSire(zdo);
            int barred = MutationBars.Of(parent.m_character); // a large kind never inherits Gilded or Relentless
            CreatureTraits child = KeeperBonus.Apply(zdo, Inheritance.Offspring(mother, sire, RuleState.Active, barred), mother, sire);
            if (Log.Diagnostics)
            {
                Log.Diag($"breeding: {parent.name} ({mother.Stars} stars, mask {mother.Mask}) and "
                    + (sire != null ? $"partner ({sire.Stars} stars, mask {sire.Mask})" : "no partner")
                    + $" -> {child.Stars} stars, mask {child.Mask}");
            }
            return new Lineage.Newborn(child, BiomeOf(zdo, parent.transform.position));
        }

        /// <summary>A young creature about to grow up: the traits it already has, carried to the adult unchanged.</summary>
        public static Lineage.Newborn? Grown(Character young)
        {
            ZDO? zdo = young.m_nview != null && young.m_nview.IsValid() ? young.m_nview.GetZDO() : null;
            if (zdo == null || !TraitStore.IsResolved(zdo))
            {
                return null;
            }
            return new Lineage.Newborn(TraitStore.Load(zdo), BiomeOf(zdo, young.transform.position));
        }

        // The pregnancy check counts partners of the partner prefab (the parent's own, unless the species pairs with a
        // separate one) within the partner range; the nearest tamed one of those is the partner.
        private static Character? FindPartner(Procreation parent)
        {
            GameObject? prefab = parent.m_seperatePartner != null ? parent.m_seperatePartner : parent.m_myPrefab;
            if (prefab == null)
            {
                return null;
            }
            Vector3 at = parent.transform.position;
            Character? best = null;
            float bestDistance = parent.m_partnerCheckRange;
            foreach (Character candidate in Character.GetAllCharacters())
            {
                float distance = Vector3.Distance(at, candidate.transform.position);
                if (distance <= bestDistance && IsPartner(candidate, parent, prefab.name))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static bool IsPartner(Character candidate, Procreation parent, string prefabName) =>
            candidate.gameObject != parent.gameObject && candidate.IsTamed()
            && Utils.GetPrefabName(candidate.gameObject) == prefabName;

        // A parent's traits: its controller's once resolved, else straight from the ZDO; a parent the mod never
        // resolved (a boss can never breed, but a modded creature might never wake the controller) counts as plain.
        private static CreatureTraits TraitsOf(Character? creature)
        {
            if (creature == null)
            {
                return new CreatureTraits(0, 0);
            }
            EliteController? controller = creature.GetComponent<EliteController>();
            if (controller != null && controller.Ready)
            {
                return controller.Traits;
            }
            ZDO? zdo = creature.m_nview != null && creature.m_nview.IsValid() ? creature.m_nview.GetZDO() : null;
            return zdo != null && TraitStore.IsResolved(zdo) ? TraitStore.Load(zdo) : new CreatureTraits(0, 0);
        }

        private static Heightmap.Biome BiomeOf(ZDO zdo, Vector3 position)
        {
            Heightmap.Biome biome = TraitStore.GetBiome(zdo);
            return biome != Heightmap.Biome.None ? biome : Heightmap.FindBiome(position);
        }
    }
}
