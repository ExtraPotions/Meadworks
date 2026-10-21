// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Chest and world drops (drops.md section 11): a world container that the game fills with default loot - dungeon
    /// and ruin chests, buried treasure, anything whose prefab carries a default drop table - also gets one roll of our
    /// stones and pre-rolled gear, put straight into its inventory with the items' full data.
    /// <para>
    /// When and where: at the moment the game fills it (<c>Container.AddDefaultItems</c>, called once from the
    /// container's <c>Awake</c> on its ZDO owner while the game's own "default items added" flag is unset), on that same
    /// owner - usually the client of the player whose area generated the container, else the server. The roll uses the
    /// server's synced tables; the container's inventory replicates through the game's own container save. Our
    /// <c>ecf_filled</c> flag is set before anything is added, so no container ever rolls twice.
    /// </para>
    /// <para>
    /// Which containers: a non-empty default drop table and no builder (the <c>creator</c> of a placed piece), so a
    /// player-built chest never rolls. Containers generated before this mod was installed were filled then and do not
    /// roll (DECISIONS IMP-76). Tier: the container entry's <c>tier</c>, else the biome at its position (dungeons report
    /// their surface biome). Chances: <c>drops.chests</c>, no stars, no killer, the per-kill caps (IMP-77).
    /// </para>
    /// </summary>
    public static class ChestRoller
    {
        private static readonly LootPlan Plan = new LootPlan();

        /// <summary>After the game added a container's default items. Owner only; returns at once for everything else.</summary>
        internal static void OnDefaultItems(Container container)
        {
            ZNetView nview = container.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return;
            }
            ZDO zdo = nview.GetZDO();
            if (!IsWorldContainer(container, zdo) || zdo.GetBool(LootKeys.FilledHash))
            {
                return;
            }
            zdo.Set(LootKeys.FilledHash, true);
            Roll(container, Utils.GetPrefabName(nview.gameObject), IsCheated(zdo));
        }

        /// <summary>A container the game fills with default loot and no player built.</summary>
        public static bool IsWorldContainer(Container container, ZDO zdo) =>
            container.m_defaultItems != null && container.m_defaultItems.m_drops.Count > 0
            && zdo.GetLong(ZDOVars.s_creator, 0L) == 0L;

        /// <summary>Plans <paramref name="chests"/> chest rolls at a tier without spawning anything (<c>ecraft</c> testing).</summary>
        public static LootSimulation Simulate(int tier, int chests, string? containerPrefab = null)
        {
            RuleSet rules = ActiveRules.Current;
            CreatureDrop? entry = containerPrefab == null ? null : ContainerEntry(rules.Economy, containerPrefab);
            LootInput input = Input(entry != null && entry.Tier > 0 ? entry.Tier : BiomeTiers.Clamp(tier), entry);
            LootSimulation result = new LootSimulation(input.Tier, 0, containerPrefab ?? "chest");
            LootPlan plan = new LootPlan();
            for (int i = 0; i < chests; i++)
            {
                LootPlanner.Plan(rules.Economy, input, LootRoller.Rng, plan);
                result.Add(plan);
            }
            return result;
        }

        private static void Roll(Container container, string prefab, bool cheated)
        {
            RuleSet rules = ActiveRules.Current;
            CreatureDrop? entry = ContainerEntry(rules.Economy, prefab);
            if (entry != null && entry.Multiplier <= 0f)
            {
                LogSkip(prefab);
                return;
            }
            int tier = entry != null && entry.Tier > 0
                ? BiomeTiers.Clamp(entry.Tier)
                : BiomeTiers.AtPosition(container.transform.position, rules.Economy, out _);
            LootPlanner.Plan(rules.Economy, Input(tier, entry), LootRoller.Rng, Plan);
            ChestFiller.Result result = ChestFiller.Fill(container.GetInventory(), Plan, cheated, rules);
            LogRoll(prefab, Plan, result);
        }

        private static LootInput Input(int tier, CreatureDrop? entry)
        {
            return new LootInput
            {
                Source = LootSource.Chest,
                Tier = tier,
                Creature = entry,
                StonesOn = ModSettings.StoneDrops.Value,
                GearOn = GearDrops.On,
                Modifiers = LootModifiers.None,
            };
        }

        // As the game marks a cheated container's default items (Container.AddDefaultItems).
        private static bool IsCheated(ZDO zdo) => zdo.GetBool(ZDOVars.s_cheated) && !PlayerProfile.s_bypassCheatChecks;

        // drops.chests.containers (economy-yaml.md section 8): container prefab -> an entry in the creature entry's shape
        // (tier, multiplier, stone_multiplier, gear_multiplier, bonus). The rules parse it once the contract request
        // "Loot, Phase 2: drops.chests.containers" is done (~/scratch/specs/ec-contract-requests.md); until then every
        // container uses its biome's tier and the flat chances.
        private static CreatureDrop? ContainerEntry(EconomyRules economy, string prefab) =>
            economy.Drops.Chests.Containers.TryGetValue(prefab, out CreatureDrop entry) ? entry : null;

        private static void LogSkip(string prefab)
        {
            if (ModSettings.LogRolls.Value)
            {
                Log.Info($"chest roll (container owner): {prefab} drops nothing: container multiplier is 0");
            }
        }

        private static void LogRoll(string prefab, LootPlan plan, ChestFiller.Result result)
        {
            if (ModSettings.LogRolls.Value)
            {
                Log.Info($"chest roll (container owner): {prefab} tier {plan.Tier} p(rune) {plan.StoneChance:0.###} "
                    + $"p(gear) {plan.GearChance:0.###} -> {result.Stones}/{plan.Stones.Count} runes, "
                    + $"{result.Gear}/{plan.Gear.Count} gear added{(result.Full ? " (container full, the rest is lost)" : "")}");
            }
        }
    }
}
