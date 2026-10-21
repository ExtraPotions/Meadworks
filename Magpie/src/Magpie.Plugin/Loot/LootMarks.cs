// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Magpie.Config;
using ExtraPotions.Magpie.Loot;
using ExtraPotions.Magpie.StepAside;
using HarmonyLib;

namespace ExtraPotions.Magpie.Runtime.Loot
{
    /// <summary>
    /// Design D2 and section 3.4 (G07, user decision Q1): while a world loot source spawns its drops a one-call scope is
    /// open, and <c>ItemDrop.Awake</c> inside it writes <c>magpie.loot</c> = 1 on the new item's ZDO, which the spawning
    /// peer owns. Marks are written only while the effective multiplier is above 1, so worlds without the cheat carry no
    /// extra data. Player drops, container and tombstone spills, refunds and station output open no scope. Whether
    /// <c>ItemDrop.Awake</c> runs inside each spawn member is verify V4 (native pending).
    /// </summary>
    internal static class LootMarks
    {
        /// <summary>The loot source whose spawn call is running, or Unknown.</summary>
        public static LootSource Current { get; private set; } = LootSource.Unknown;

        public static LootSource Open(LootSource source)
        {
            var previous = Current;
            Current = source;
            return previous;
        }

        public static void Close(LootSource previous) => Current = previous;

        /// <summary>Marks a freshly created item when the open scope is world loot the settings multiply.</summary>
        public static void MarkIfLoot(ItemDrop item)
        {
            var source = Current;
            if (source == LootSource.Unknown || !MagpieRuntime.ModuleOn(MagpieModule.Loot)) return;
            var settings = MagpieRuntime.Settings!;
            var multiplier = settings.Get<int>(MagpieSettings.LootMultiplier);
            var sources = settings.Get<LootSources>(MagpieSettings.LootSources);
            float creatureMultiplier = source == LootSource.Creature ? WildbloodMultiplier.Read() : 1f;
            if (!LootPolicy.ShouldMark(source, sources, multiplier, creatureMultiplier)) return;
            var view = item.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                MagpieRuntime.Log?.Debug("Loot mark skipped: no owned ZDO during Awake (" + source + ").");
                return;
            }
            view.GetZDO().Set(MagpieCore.LootMarkKey, 1);
        }

        public static bool IsMarked(ItemDrop item)
        {
            var view = item != null ? item.m_nview : null;
            return view != null && view.IsValid() && view.GetZDO().GetInt(MagpieCore.LootMarkKey, 0) == 1;
        }
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
    internal static class LootMarkAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDrop __instance)
        {
            try { LootMarks.MarkIfLoot(__instance); }
            catch (Exception e) { MagpieRuntime.Log?.Error("Loot mark failed: " + e); }
        }
    }

    // Scopes per loot source (design section 7, members verified at compile time; V4 confirms in game that the drops
    // are created inside these calls). Each prefix opens the scope and keeps the previous one; the finalizer restores it
    // even when the original throws.

    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.DropItems))]
    internal static class CreatureLootScope
    {
        private static void Prefix(out LootSource __state) => __state = LootMarks.Open(LootSource.Creature);
        private static void Finalizer(LootSource __state) => LootMarks.Close(__state);
    }

    [HarmonyPatch(typeof(DropOnDestroyed), nameof(DropOnDestroyed.OnDestroyed))]
    internal static class DestructibleLootScope
    {
        private static void Prefix(out LootSource __state) => __state = LootMarks.Open(LootSource.Destructible);
        private static void Finalizer(LootSource __state) => LootMarks.Close(__state);
    }

    [HarmonyPatch(typeof(MineRock), nameof(MineRock.RPC_Hit))]
    internal static class MineRockLootScope
    {
        private static void Prefix(out LootSource __state) => __state = LootMarks.Open(LootSource.RockOrTree);
        private static void Finalizer(LootSource __state) => LootMarks.Close(__state);
    }

    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.DamageArea))]
    internal static class MineRock5LootScope
    {
        private static void Prefix(out LootSource __state) => __state = LootMarks.Open(LootSource.RockOrTree);
        private static void Finalizer(LootSource __state) => LootMarks.Close(__state);
    }

    [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.RPC_Damage))]
    internal static class TreeLootScope
    {
        private static void Prefix(out LootSource __state) => __state = LootMarks.Open(LootSource.RockOrTree);
        private static void Finalizer(LootSource __state) => LootMarks.Close(__state);
    }

    [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Destroy))]
    internal static class TreeLogLootScope
    {
        private static void Prefix(out LootSource __state) => __state = LootMarks.Open(LootSource.RockOrTree);
        private static void Finalizer(LootSource __state) => LootMarks.Close(__state);
    }

    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Drop))]
    internal static class PickableLootScope
    {
        private static void Prefix(out LootSource __state) => __state = LootMarks.Open(LootSource.Pickable);
        private static void Finalizer(LootSource __state) => LootMarks.Close(__state);
    }

    [HarmonyPatch(typeof(LootSpawner), nameof(LootSpawner.UpdateSpawner))]
    internal static class SpawnerLootScope
    {
        private static void Prefix(out LootSource __state) => __state = LootMarks.Open(LootSource.Spawner);
        private static void Finalizer(LootSource __state) => LootMarks.Close(__state);
    }
}
