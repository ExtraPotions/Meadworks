// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Entry point of the Loot area, called once from plugin Awake after the rules, settings and words are loaded.
    /// Owns: creature stone drops and pre-rolled magic gear drops on the dying creature's owner (drops.md); chest and
    /// world-container drops on the container's owner (section 11); the loot-find stats (section 10) - published by each
    /// player's own client to its player ZDO, read by the creature's owner at the kill.
    /// The patches are <see cref="DeathPatch"/> (the roll, killer stats), <see cref="AllyHits"/> (<c>ecf_ally_hit</c>, called from the damage dispatcher),
    /// <see cref="GenerateDropListPatch"/> (Trophy Taker, Hoardfinder), <see cref="RagdollSetupPatch"/> /
    /// <see cref="RagdollSpawnLootPatch"/> (our drops ride the ragdoll, <see cref="CorpseLoot"/>), <see cref="ChestFillPatch"/> (chests); the find stats are
    /// republished after each effects rebuild (<see cref="FindPublisher"/>).
    /// Tables: the per-tier stone and rarity draws are precomputed by the rules (<c>EconomyRules.StoneDraw</c>,
    /// <c>GearRarityDraw</c>); the creature profiles and the gear pool are derived here and dropped on every rules change
    /// (the gear pool is built again soon after, a slice a frame, by <see cref="GearPoolWarmup"/>).
    /// </summary>
    public static class LootFeature
    {
        public static void Init()
        {
            ActiveRules.RulesChanged += OnRulesChanged;
            FindPublisher.Install();
            GearPoolWarmup.Create();
        }

        private static void OnRulesChanged()
        {
            CreatureProfiles.Clear();
            GearPool.Invalidate();
        }
        internal static void Shutdown()
        {
            ActiveRules.RulesChanged -= OnRulesChanged;
            FindPublisher.Uninstall();
        }
    }
}
