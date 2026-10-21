// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Adapters;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Breeding
{
    /// <summary>
    /// Carries a newborn's traits across the one game call that creates it: a birth, a hatching, or a young creature
    /// growing up. Awake identifies the target, the postfix carries only its blob after the game supplies the level,
    /// and the finalizer closes this exact context even on failure. Nested calls restore their outer context.
    /// No target means no inheritance, egg write or parent-record change. Runs on the game thread.
    /// </summary>
    internal static class Lineage
    {
        /// <summary>What a newborn is: its traits, and the biome whose rules it follows - its parent's, not where it stands.</summary>
        public sealed class Newborn
        {
            public readonly CreatureTraits Traits;
            public readonly Heightmap.Biome Biome;

            public Newborn(CreatureTraits traits, Heightmap.Biome biome)
            {
                Traits = traits;
                Biome = biome;
            }
        }

        internal sealed class Call
        {
            internal readonly Call? Previous;
            internal readonly Func<int, string> Source;
            internal readonly string Prefab;
            internal readonly bool WatchEggs;
            internal Character? Creature;
            internal ItemDrop? Egg;
            internal bool Completed;
            internal Call(Call? previous, Func<int, string> source, string prefab, bool watchEggs)
            { Previous = previous; Source = source; Prefab = prefab; WatchEggs = watchEggs; }
        }

        private static Call? current;

        internal static Call Begin(Func<int, string> source, string prefab, bool watchEggs = false)
        {
            var call = new Call(current, source, prefab, watchEggs);
            current = call;
            return call;
        }

        internal static void End(Call? call)
        {
            if (call != null && ReferenceEquals(current, call)) current = call.Previous;
        }

        /// <summary>Awake captures the target; the enclosing game's call supplies its level before Complete.</summary>
        public static void Claim(Character character)
        {
            Call? call = current;
            if (call != null && ShepherdBreedingOwnership.FallbackActive && call.Creature == null && call.Egg == null
                && !character.IsPlayer() && Utils.GetPrefabName(character.gameObject) == call.Prefab)
                call.Creature = character;
        }

        public static void Notice(ItemDrop item)
        {
            Call? call = current;
            if (call != null && ShepherdBreedingOwnership.FallbackActive && call.WatchEggs && call.Egg == null
                && call.Creature == null && Utils.GetPrefabName(item.gameObject) == call.Prefab)
                call.Egg = item;
        }

        /// <summary>Only trait state is applied, once, after native SetLevel/SetQuality; source eggs stay intact.</summary>
        internal static void Complete(Call? call)
        {
            if (call == null || call.Completed || !ReferenceEquals(current, call)) return;
            call.Completed = true;
            if (!ShepherdBreedingOwnership.FallbackActive) return;
            try
            {
                if (call.Creature != null && Owned(call.Creature.m_nview))
                    NativeTraitsCapability.Apply(call.Creature, call.Source(call.Creature.GetLevel()));
                else if (call.Egg != null && Owned(call.Egg.m_nview) && call.Egg.GetComponent<EggGrow>() != null)
                {
                    var data = call.Egg.m_itemData.m_customData;
                    if (data.ContainsKey(TraitKeys.Blob) || data.ContainsKey(TraitKeys.EggTraits)
                        || data.ContainsKey(TraitKeys.Legacy(TraitKeys.EggTraits))) return;
                    string blob = call.Source(call.Egg.m_itemData.m_quality);
                    data[TraitKeys.Blob] = NativeTraitsCapability.EggDecode(NativeTraitsCapability.EggEncode(blob));
                    call.Egg.Save();
                }
            }
            catch (Exception error) { Guard.Report(error, "breeding: minimum trait carry"); }
        }

        private static bool Owned(ZNetView view) => view != null && view.IsValid() && view.IsOwner();
    }
}
