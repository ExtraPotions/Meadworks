// Adapted from EliteCreaturesReborn 3.30.0 Breeding/EggData.cs and Patches/BreedingPatch.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 0471a49e972e4453a453e365cb90b5fd20d0f4ea
// Changed by expDARE, 2026: Shepherd lineage and optional Wildblood traits; preserve native levels when Wildblood declines stars.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Runtime.Integration;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Breeding
{
    internal static class OffspringStamp
    {
        private static readonly string InstanceKey = BreedingKeys.Instance;
        private static bool Owned(ZNetView view) => view != null && view.IsValid() && view.IsOwner();

        /// <summary>Writes the plan on the claimed newborn: level (after the game's own SetLevel/SetQuality) and lineage or egg record.</summary>
        internal static void Apply(GameObject born, BirthPlan plan, bool twin, bool inheritance, bool stars)
        {
            Character? character = born.GetComponent<Character>();
            if (character != null)
            {
                ZNetView view = character.m_nview;
                if (!Owned(view)) return;
                if (inheritance && stars && WildbloodOffspringStars.Allow()) character.SetLevel(plan.Level);
                var store = new ZdoKeyStore(view.GetZDO());
                if (plan.Lineage != null)
                {
                    store.Set(InstanceKey, plan.Lineage.Token);
                    BreedingStore.WriteLineage(store, plan.Lineage);
                }
                if (inheritance)
                {
                    BreedingStore.SetTraits(store, plan.Egg.Traits);
                    WildbloodTraitsCapability.Current.Apply(character, plan.Egg.Traits);
                }
                if (twin) BreedingStore.SetIsTwin(store, true);
                return;
            }
            ItemDrop? egg = born.GetComponent<ItemDrop>();
            if (egg == null || !Owned(egg.m_nview)) return;
            if (inheritance && stars && WildbloodOffspringStars.Allow()) egg.SetQuality(plan.EggQuality);
            var traits = WildbloodTraitsCapability.Current;
            if (plan.Lineage != null || inheritance && (traits.Available || plan.Egg.Traits.Length != 0))
            {
                egg.m_itemData.m_customData[BreedingKeys.Egg] = inheritance ? traits.EncodeEgg(plan.Egg) : EggCodec.Encode(plan.Egg);
                egg.Save();
            }
        }
    }
}
