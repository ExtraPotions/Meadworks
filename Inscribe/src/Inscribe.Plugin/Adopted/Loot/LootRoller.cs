// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// The creature drop roll (drops.md): qualify the death, resolve the creature's profile and tier, plan with
    /// <see cref="LootPlanner"/>, then spawn stones and pre-rolled gear. Also the entry points for <c>ecraft</c>:
    /// <see cref="Simulate"/> (counts only) and <see cref="SpawnAt"/> (real drops at a position).
    /// <para>
    /// Where it runs: <see cref="OnDeath"/> is called from the death hook on the dying creature's ZDO owner only. On a
    /// dedicated server that is often a nearby player's client rather than the server (multiplayer.md section 2); that
    /// is fine, because the tables it reads are the server's synced rules and the drops it spawns are ordinary world
    /// items whose data is in their ZDO. One roll per death, never one per client.
    /// </para>
    /// </summary>
    public static class LootRoller
    {
        /// <summary>The one generator every drop roll draws from (creatures and chests): independent draws per roll (IMP-2).</summary>
        internal static readonly System.Random Rng = new System.Random();
        private static readonly LootPlan Plan = new LootPlan();

        /// <summary>
        /// The death hook. Owner only; returns at once for players, tamed and uninvolved deaths. <paramref name="modifiers"/>
        /// are the killer's loot-find totals (<see cref="KillerStats"/>).
        /// </summary>
        internal static void OnDeath(Character creature, LootModifiers modifiers)
        {
            if (!DeathFacts.TryRead(creature, out DeathFacts facts) || !creature.m_nview.IsOwner())
            {
                return;
            }
            RuleSet rules = ActiveRules.Current;
            CreatureProfile profile = CreatureProfiles.Get(creature, facts.PrefabHash, rules);
            if (!Qualifies(facts, profile, rules.Economy.Drops))
            {
                return;
            }
            EcrWatch.Record(facts.Ecr);
            LootInput input = InputFor(profile, facts, rules.Economy, modifiers);
            LootPlanner.Plan(rules.Economy, input, Rng, Plan);
            CorpseLoot.Begin(facts.Center);   // held for the ragdoll; the death hook's finalizer drops what it did not take
            int gear = SpawnPlan(Plan, facts.Center, facts.Cheated, rules, creatureDrops: true);
            LogRoll(profile.Prefab, input, Plan, gear);
        }

        private static bool Qualifies(DeathFacts facts, CreatureProfile profile, DropRules drops)
        {
            string reason = "creature multiplier is 0";
            if (!profile.Blocked && facts.Qualifies(drops, out reason))
            {
                return true;
            }
            if (ModSettings.LogRolls.Value)
            {
                Log.Info($"drop roll (creature owner): {profile.Prefab} drops nothing: {reason}");
            }
            return false;
        }

        /// <summary>
        /// The roll input for a death: tier from the profile, else from the death position; stars and the ECR terms from
        /// the facts (Elite Creatures Reborn's stars for a resolved creature with the synergy on, ecr-integration.md 4).
        /// </summary>
        public static LootInput InputFor(CreatureProfile profile, DeathFacts facts, EconomyRules economy, LootModifiers modifiers)
        {
            int tier = profile.Tier > 0 ? profile.Tier : BiomeTiers.AtPosition(facts.Position, economy, out _);
            if (Runtime.InscribeLootInterop.StarSynergy && facts.Wildblood?.Item5 > 0 && profile.Tier <= 0) tier = BiomeTiers.Clamp(facts.Wildblood.Item5);
            LootInput input = Input(tier, facts.RollStars, profile.Boss, facts.FlaggedBoss, profile.Creature, modifiers);
            input.Ecr = facts.Ecr.Roll;
            input.Prefab = profile.Prefab;
            return input;
        }

        /// <summary>
        /// Plans <paramref name="kills"/> kills at a tier without spawning anything (<c>ecraft</c> testing). A
        /// <paramref name="creaturePrefab"/> uses that creature's boss and creature entries (its tier when it has one);
        /// <paramref name="find"/> stands for a killer's loot-find totals (Fateweaver, Norns' Favour).
        /// </summary>
        public static LootSimulation Simulate(int tier, int stars, int kills, string? creaturePrefab = null, LootModifiers? find = null)
        {
            RuleSet rules = ActiveRules.Current;
            LootInput input = TestInput(tier, stars, creaturePrefab, rules.Economy);
            input.Modifiers = find ?? LootModifiers.None;
            LootSimulation result = new LootSimulation(input.Tier, stars, creaturePrefab);
            LootPlan plan = new LootPlan();
            for (int i = 0; i < kills; i++)
            {
                LootPlanner.Plan(rules.Economy, input, Rng, plan);
                result.Add(plan);
            }
            return result;
        }

        /// <summary>Rolls one kill at a tier and spawns the result at <paramref name="center"/>. Returns the plan.</summary>
        public static LootPlan SpawnAt(Vector3 center, int tier, int stars, string? creaturePrefab = null)
        {
            RuleSet rules = ActiveRules.Current;
            LootPlan plan = new LootPlan();
            LootPlanner.Plan(rules.Economy, TestInput(tier, stars, creaturePrefab, rules.Economy), Rng, plan);
            SpawnPlan(plan, center, cheated: false, rules);
            return plan;
        }

        private static LootInput TestInput(int tier, int stars, string? creaturePrefab, EconomyRules economy)
        {
            CreatureProfile? profile = creaturePrefab == null ? null : CreatureProfiles.Build(creaturePrefab, economy);
            int useTier = profile != null && profile.Tier > 0 ? profile.Tier : BiomeTiers.Clamp(tier);
            LootInput input = Input(useTier, stars, profile?.Boss, false, profile?.Creature, LootModifiers.None);
            input.Prefab = creaturePrefab;
            return input;
        }

        private static LootInput Input(int tier, int stars, BossDrop? boss, bool flaggedBoss, CreatureDrop? creature,
            LootModifiers modifiers)
        {
            return new LootInput
            {
                Tier = tier,
                Stars = stars,
                IsBoss = flaggedBoss || boss != null,
                Boss = boss,
                Creature = creature,
                StonesOn = ModSettings.StoneDrops.Value,
                GearOn = GearDrops.On,
                Modifiers = modifiers,
            };
        }

        // Returns how many gear items actually dropped.
        private static int SpawnPlan(LootPlan plan, Vector3 center, bool cheated, RuleSet rules, bool creatureDrops = false)
        {
            int dropped = 0;
            WeightedTable<GearBase> bases = plan.Gear.Count > 0 ? GearPool.ForTier(plan.Tier) : WeightedTable<GearBase>.Empty;
            foreach (RarityDef rarity in plan.Gear)
            {
                if (!bases.TryPick((float)Rng.NextDouble(), out GearBase gearBase))
                {
                    break;
                }
                ItemDrop.ItemData? item = GearFactory.Build(gearBase, rarity, cheated, Rng, rules);
                if (item != null)
                {
                    LootSpawner.Drop(item, 1, center);
                    dropped++;
                }
            }
            LootSpawner.DropStones(plan.Stones, center, cheated, creatureDrops);
            return dropped;
        }

        private static void LogRoll(string prefab, LootInput input, LootPlan plan, int gear)
        {
            if (ModSettings.LogRolls.Value)
            {
                LootModifiers find = input.Modifiers;
                string ecr = input.Ecr.StarsFromEcr ? " (ECR)" : "";
                ecr += input.Ecr.HasTier ? $" ECR world tier {input.Ecr.Tier}" : "";
                Log.Info($"drop roll (creature owner): {prefab} stars {input.Stars}{ecr} tier {plan.Tier}{(plan.IsBoss ? " boss" : "")} "
                    + $"p(rune) {plan.StoneChance:0.###} p(gear) {plan.GearChance:0.###} -> {plan.Stones.Count} runes, "
                    + $"{gear}/{plan.Gear.Count} gear; killer find: rarity +{find.RarityBonusPercent:0.#}%, "
                    + $"runes +{find.StonesPercent:0.#}%, trophy +{find.TrophyPercent:0.#}%, coins +{find.CoinsPercent:0.#}%");
            }
        }
    }
}
