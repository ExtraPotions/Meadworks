// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: which Harmony hooks Wildblood's startup activates. Never PatchAll: only Wildblood's own Runtime
// hooks and explicitly reviewed native groups; the hooks whose features Delve owns are excluded for good.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Integration
{
    /// <summary>An adopted hook that is never activated, the product that owns its feature and why.</summary>
    public sealed class HookExclusion
    {
        public HookExclusion(string typeName, string path, string owner, string reason)
        {
            TypeName = typeName;
            Path = path;
            Owner = owner;
            Reason = reason;
        }

        /// <summary>The Harmony patch class name (no namespace).</summary>
        public string TypeName { get; }

        /// <summary>Repository-relative source file (kept byte for byte with its upstream notice).</summary>
        public string Path { get; }

        public string Owner { get; }
        public string Reason { get; }

        public override string ToString() => TypeName + " (" + Owner + ")";
    }

    public static class HookActivation
    {
        /// <summary>The namespace of Wildblood's own hooks; startup activates only these.</summary>
        public const string RuntimeNamespace = "ExtraPotions.Wildblood.Runtime";

        private const string Patches = "src/Wildblood.Plugin/Native/Patches/";

        /// <summary>Adopted Elite Creatures Reborn hooks whose features moved to Delve. The files stay (provenance and
        /// notices intact), but no activation path may install them.</summary>
        public static IReadOnlyList<HookExclusion> Excluded { get; } = Array.AsReadOnly(new[]
        {
            new HookExclusion("SpawnerRespawnPatch", Patches + "SpawnerRespawnPatch.cs", "Delve",
                "camp and dungeon spawner respawn is Delve's Creature Respawn (DV15)"),
            new HookExclusion("SpawnerFirstSpawnPatch", Patches + "SpawnerRespawnPatch.cs", "Delve",
                "camp and dungeon spawner respawn is Delve's Creature Respawn (DV15)"),
            new HookExclusion("DungeonLootPatch", Patches + "DungeonLootPatch.cs", "Delve",
                "dungeon chest refill is Delve's (its converter maps the dungeon loot timer); both refilling one chest must not happen"),
        });

        /// <summary>Exact reviewed native foundation classes; namespace lookalikes never inherit admission.</summary>
        public static IReadOnlyList<string> NativeFoundation { get; } = Array.AsReadOnly(new[]
        {
            "ExtraPotions.Wildblood.Adopted.Patches.CharacterLifecyclePatch",
            "ExtraPotions.Wildblood.Adopted.Horde.HordeSpawnPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.NativeLevelDamagePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.HitPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.LootPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.DeathPatch",
            "ExtraPotions.Wildblood.Adopted.Runtime.BossProgressSetGuard",
            "ExtraPotions.Wildblood.Adopted.Runtime.BossProgressRemoveGuard",
            "ExtraPotions.Wildblood.Adopted.Runtime.BossRewardDropScope",
            "ExtraPotions.Wildblood.Adopted.Runtime.BossRewardRagdollScope",
            "ExtraPotions.Wildblood.Adopted.Patches.DeathRagdollPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.TierRisePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.TierStartPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.TierKeysPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.WorldExitPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.AltarStartPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.AltarHoverPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.AltarLockPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.AltarSpawnPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BirthPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.EggLaidPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.GrowUpPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.HatchPatch",
            "ExtraPotions.Wildblood.Adopted.Scaling.NativeLevelHealthPatch",
            "ExtraPotions.Wildblood.AdoptedCm.NativePseudoChancePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureTargetingPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureTargetingDestroyPatch",
            "ExtraPotions.Wildblood.AdoptedCm.NativePressureSavePatch",
            "ExtraPotions.Wildblood.AdoptedCm.NativePressureShutdownPatch",
            "ExtraPotions.Wildblood.AdoptedLuck.CharacterDropPatch",
            "ExtraPotions.Wildblood.AdoptedLuck.PlayerLoadPatch",
            "ExtraPotions.Wildblood.AdoptedLuck.PlayerSavePatch",
            "ExtraPotions.Wildblood.AdoptedLuck.PlayerSpawnPatch",
            "ExtraPotions.Wildblood.AdoptedLuck.PlayerDestroyPatch",
            "ExtraPotions.Wildblood.AdoptedLuck.PlayerLuckRpcPatch",
        });
        /// <summary>Reviewed remaining source callbacks; raw legacy console and Delve owners stay excluded.</summary>
        public static IReadOnlyList<string> NativeEffects { get; } = Array.AsReadOnly(new[]
        {
            "ExtraPotions.Wildblood.Adopted.Patches.LoadedWeaponUnequipPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.LoadedWeaponEquipPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.AnimSpeedPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BossBoardDeathPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BossBoardStartPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BossDamagePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BossHintDeathPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BossHintStartPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BrutalBlockPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BrutalBlowPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BrutalFallPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BrutalFlightPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.BrutalMarkPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.CloneAttackPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.CloneHitPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.ColossalAttackPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.ColossalCorpseMadePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.ColossalCorpseWakePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.CorrodentPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.EchoAimPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.EchoAttackPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.EchoCuePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.EnemyPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.FrostHealPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.GildedEnemyPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.GildedFleePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.GildedFriendPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.GraviticPullPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.MotionPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.IceFrictionPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.IceSlipPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.JuggernautPushbackPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.JuggernautStaggerDamagePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.JuggernautStaggerPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.NightfallChillPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.NightfallSkyPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.PiercingArmourPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.PortalSpawnPointPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.PortalStartPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessCrouchPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessDayPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessNoisePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessRetargetPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessSleepPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessStealthPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessSummonPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessTargetPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessThinkPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RelentlessUnarmedPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.ShriekCastPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.ShriekRpcPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.StarCorpseMadeHumanoidPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.StarCorpseMadePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.StarCorpseWakePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.StarLookColorPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.ThievingDespawnPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.ThievingParryPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerAttackStartSpeedPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerBurningTickScopePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerCharacterAnimEventSpeedPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerCharacterApplyPushbackPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerCharacterBlockStaggerDamagePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerCharacterHealPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerCharacterModifierUpdatePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerCharacterOnDeathPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerCharacterRpcDamagePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerCharacterSetHealthPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerCreatureSpawnerBlockPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerFireSourcePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerHumanoidBlockStaggerScopePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerHumanoidGetTimeSinceLastAttackPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerMonsterAiBlamerFleePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerMonsterAiBlinkAlertPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerMonsterAiBlinkRangePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerObjectDbAwakeStatusPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerObjectDbCopyStatusPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerPlayerControlDebuffUpdatePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerPlayerGetBodyArmorPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerPlayerOnDeathPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerPlayerOnRespawnPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerPoisonSourcePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerPoisonTickScopePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerSpawnAreaBlockPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerSpawnBlockQueryPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerSpawnPointBlockPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerSpawnerGroupBlockPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerSpiritSourcePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerUndodgeableAoeScopePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerUndodgeableAttackScopePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerUndodgeableDamageDispatchPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerUndodgeableDamageScopePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerUndodgeableDodgeQueryPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerUndodgeableProjectileScopePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerVortexDirectProjectileDamagePatch",
            "ExtraPotions.Wildblood.AdoptedCm.CreatureManagerVortexRpcDamageMarkerPatch",

        });
        public static IReadOnlyList<string> NativeRecap { get; } = Array.AsReadOnly(new[]
        {
            "ExtraPotions.Wildblood.Adopted.Patches.RecapHitPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.RecapDeathPatch",
        });
        public static IReadOnlyList<string> NativeDisplay { get; } = Array.AsReadOnly(new[]
        {
            "ExtraPotions.Wildblood.Adopted.Patches.EnemyHudPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.EnemyHudCloakPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.HoverNamePatch",
            "ExtraPotions.Wildblood.Adopted.Patches.EggHoverPatch",
            "ExtraPotions.Wildblood.Adopted.Patches.EggTooltipPatch",
        });
        /// <summary>Exact reviewed local transaction guards; their separate Harmony owner can outlive a stopped product only while its exact native inventory is held.</summary>
        public static IReadOnlyList<string> NativeTheft { get; } = Array.AsReadOnly(new[]
        {
            "ExtraPotions.Wildblood.Adopted.Transfers.TheftProfileLoadGuard",
            "ExtraPotions.Wildblood.Adopted.Transfers.TheftDeathGuard",
            "ExtraPotions.Wildblood.Adopted.Transfers.TheftInventoryWritesGuard",
            "ExtraPotions.Wildblood.Adopted.Transfers.TheftInventoryLoadsGuard",
            "ExtraPotions.Wildblood.Adopted.Transfers.TheftPlayerActionsGuard",
        });
        /// <summary>The Compendium lore pages: one list producer and one selected-page refresh on the game's TextsDialog.</summary>
        public static IReadOnlyList<string> NativeLore { get; } = Array.AsReadOnly(new[]
        {
            "ExtraPotions.Wildblood.AdoptedCm.CompendiumListPatch",
            "ExtraPotions.Wildblood.AdoptedCm.CompendiumShowPatch",
        });
        public static IReadOnlyList<string> NativeRaids { get; } = Array.AsReadOnly(new[]
        {
            "ExtraPotions.Wildblood.Adopted.Raids.GearTierPatch",
            "ExtraPotions.Wildblood.Adopted.Raids.RaidCoinPatch",
            "ExtraPotions.Wildblood.Adopted.Raids.RaiderAttachPatch",
            "ExtraPotions.Wildblood.Adopted.Raids.RaiderLeavePatch",
            "ExtraPotions.Wildblood.Adopted.Raids.RaidNetPatch",
            "ExtraPotions.Wildblood.Adopted.Raids.GameRaidSpacingPatch",
            "ExtraPotions.Wildblood.Adopted.Raids.RaidDropPatch",
        });
        private static readonly HashSet<string> NativeNames = new HashSet<string>(NativeFoundation.Concat(NativeEffects).Concat(NativeRecap).Concat(NativeDisplay).Concat(NativeTheft).Concat(NativeLore).Concat(NativeRaids), StringComparer.Ordinal);

        public static bool IsExcluded(string typeName) => Excluded.Any(e => string.Equals(e.TypeName, typeName, StringComparison.Ordinal));

        /// <summary>Whether startup may install the Harmony patch class <paramref name="typeName"/> of
        /// <paramref name="ns"/>: a class with a Harmony attribute in the reviewed exact native group or Wildblood Runtime namespace,
        /// and never an excluded or superseded one.</summary>
        public static bool MayActivate(string? ns, string typeName, bool hasHarmonyPatch)
        {
            if (!hasHarmonyPatch || ns == null || IsExcluded(typeName)) return false;
            // The adopted loot engine is the sole multiplier owner once LootPatch is installed.
            if (ns == RuntimeNamespace && typeName == "GenerateDropListPatch") return false;
            return NativeNames.Contains(ns + "." + typeName)
                || ns == RuntimeNamespace || ns.StartsWith(RuntimeNamespace + ".", StringComparison.Ordinal);
        }
    }
}
