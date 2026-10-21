// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Raids;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Splintering's split. When a Splintering creature dies it breaks into two copies of its own prefab, each rolling
    /// its own star count from the fixed reduction table and keeping the parent's other mutations and biome. A copy
    /// landing on 0 stars drops Splintering and terminates its branch. The two optional caps - read from the rules,
    /// 0 meaning off - truncate a cascade here rather than preventing the mutation. Runs on the dying creature's owner
    /// (the death patch is owner-gated). It takes its inputs as a snapshot from <see cref="Patches.DeathPatch"/> rather
    /// than the parent's ZDO, because by the time OnDeath's postfix runs the parent's ZDO has been reset to null - the
    /// very fault that made the old build silently never split. It Instantiates the copies here, so they are owned by
    /// this machine and replicate like any spawn, and writes each copy's traits to its ZDO before the copy's own
    /// controller wakes - so the copy is born already-resolved and its owner never rolls it. Every client then reads it.
    /// A tamed parent's copies are tamed the same way, by this owner, from the <see cref="SplinterTame"/> in the snapshot.
    /// </summary>
    public static class Splitter
    {
        public static void Split(Vector3 pos, Quaternion rot, int prefabHash, CreatureTraits parentTraits,
            BiomeRules rules, int generation, string root, Heightmap.Biome biome, SplinterTame? tame, RaiderLineage? raid = null)
        {
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabHash) : null;
            if (prefab == null)
            {
                if (Log.Diagnostics)
                {
                    Log.Diag($"split aborted: no prefab for hash {prefabHash} (scene missing or prefab unknown)");
                }
                return;
            }
            int nextGen = generation + 1;
            if (Truncated(rules, nextGen, root))
            {
                if (Log.Diagnostics)
                {
                    Log.Diag($"split truncated at gen {nextGen}, root {root} (a cascade cap is on)");
                }
                return;
            }
            SpawnCopy(prefab, pos, rot, parentTraits, nextGen, root, biome, tame, raid);
            SpawnCopy(prefab, pos, rot, parentTraits, nextGen, root, biome, tame, raid);
        }

        private static bool Truncated(BiomeRules rules, int nextGen, string root)
        {
            int maxGen = (int)rules.PowerOf(Mutation.Splintering, Fields.MaxGenerations);
            int maxDesc = (int)rules.PowerOf(Mutation.Splintering, Fields.MaxDescendants);
            if (maxGen > 0 && nextGen > maxGen)
            {
                return true;
            }
            return maxDesc > 0 && DescendantRegistry.Count(root) >= maxDesc;
        }

        private static void SpawnCopy(GameObject prefab, Vector3 parentPos, Quaternion rot,
            CreatureTraits parentTraits, int gen, string root, Heightmap.Biome biome, SplinterTame? tame, RaiderLineage? raid)
        {
            int stars = SplinterTable.RollChildStars(parentTraits.Stars);
            int mask = stars == 0 ? parentTraits.Mask & ~(1 << (int)Mutation.Splintering) : parentTraits.Mask;
            Vector3 pos = parentPos + Random.insideUnitSphere * 0.5f;
            pos.y = parentPos.y;
            GameObject copy = Object.Instantiate(prefab, pos, rot);
            Configure(copy, new CreatureTraits(stars, mask), gen, root, biome, tame, raid);
        }

        private static void Configure(GameObject copy, CreatureTraits traits, int gen, string root,
            Heightmap.Biome biome, SplinterTame? tame, RaiderLineage? raid)
        {
            ZNetView nview = copy.GetComponent<ZNetView>();
            Character character = copy.GetComponent<Character>();
            if (nview == null || character == null || nview.GetZDO() == null)
            {
                Log.Diag("split copy has no valid ZNetView/ZDO after Instantiate - the copy will roll fresh, not inherit");
                return;
            }
            ZDO zdo = nview.GetZDO();
            raid?.Apply(zdo);
            TraitStore.Save(zdo, traits);
            TraitStore.SetBiome(zdo, biome);
            zdo.Set(TraitKeys.Generation, gen);
            zdo.Set(TraitKeys.CascadeRoot, root);
            character.SetLevel(1);
            tame?.Apply(copy, zdo);
            DescendantRegistry.Register(root, copy);
            if (Log.Diagnostics)
            {
                Log.Diag($"split copy spawned: {copy.name} stars={traits.Stars} mask={traits.Mask} gen={gen} splinters={traits.Has(Mutation.Splintering)} tamed={character.IsTamed()}");
            }
        }
    }
}
