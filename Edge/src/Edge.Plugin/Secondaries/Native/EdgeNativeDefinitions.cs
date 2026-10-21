// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Product boundary for the pinned world apply engine, runtime weapon rebind and owned native lifecycle.
using System;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Secondaries.Adopted;
namespace ExtraPotions.Edge.Runtime.Secondaries.Native;

internal static class EdgeNativeDefinitions
{
    private static ObjectDB? previousDb;
    private static ZNetScene? previousScene;
    private static int previousSnapshot = -1, previousItemCount = -1;
    private static string previousChoices = "";
    private static bool haveChoices;
    private static RangedPreset prevOne, prevFire, prevRapid, prevLightning, prevBow, prevCrossbow;
    private static BombPreset prevBomb;
    private static SummonQualityPreset prevSummon;
    private static int charactersDueFrame = -1;
    internal static bool IsRebinding => charactersDueFrame >= UnityEngine.Time.frameCount;
    internal static SecondaryAttackAppliedWorldSnapshot AppliedSnapshot { get; private set; } = SecondaryAttackAppliedWorldSnapshot.Empty;
    internal static System.Collections.Generic.IReadOnlyDictionary<string, SecondaryAttackDefinition> Applied => AppliedSnapshot.DefinitionsByPrefabName;
    internal static int ApplyRevision => AppliedSnapshot.ApplyRevision;
    internal static System.Collections.Generic.IReadOnlyDictionary<string, SecondaryAttackDefinition> Candidates => Applied;

    internal static void Initialize() => HunterLegacyCompat.Initialize();
    private static string? previousLifetime;
    internal static void Tick(bool force = false)
    {
        var settings = Plugin.Settings;
        var db = ObjectDB.instance;
        if (settings == null || db == null || !EdgeModules.Active(EdgeModule.Secondaries)) { Reset(); return; }
        var snapshot = EdgeRuleFiles.Current;
        var one = settings.Get(EdgeSettings.Ranged.OneHandedElementalPreset);
        var fire = settings.Get(EdgeSettings.Ranged.FireballStaffPreset);
        var rapid = settings.Get(EdgeSettings.Ranged.RapidfireStaffPreset);
        var lightning = settings.Get(EdgeSettings.Ranged.LightningStaffPreset);
        var bow = settings.Get(EdgeSettings.Ranged.BowPreset);
        var crossbow = settings.Get(EdgeSettings.Ranged.CrossbowPreset);
        var bomb = settings.Get(EdgeSettings.Ranged.BombPreset);
        var summon = settings.Get(EdgeSettings.BloodMagic.SummonQualityPreset);
        var sameChoices = haveChoices && one == prevOne && fire == prevFire && rapid == prevRapid && lightning == prevLightning
            && bow == prevBow && crossbow == prevCrossbow && bomb == prevBomb && summon == prevSummon;
        var scene = ZNetScene.instance;
        if (!force && db == previousDb && scene == previousScene && snapshot.SnapshotId == previousSnapshot &&
            sameChoices && db.m_items.Count == previousItemCount)
        {
            if (charactersDueFrame >= 0 && UnityEngine.Time.frameCount > charactersDueFrame)
            { SecondaryAttacksCharacterRpc.EnsureCharacters(); charactersDueFrame = -1; }
            SubSummonSystem.RegisterRpc(ZRoutedRpc.instance);
            // Lifetime settings also apply to summons that already exist, without re-applying the presets.
            var lifetime = settings.Get(EdgeSettings.BloodMagic.SummonLifetimeSeconds) + "/" + settings.Get(EdgeSettings.BloodMagic.LifetimeMultiplierAtSkill100);
            if (lifetime != previousLifetime) { previousLifetime = lifetime; MagicSummonQualityPresetSystem.RefreshLoadedSummonLifetimeState(); }
            return;
        }
        var key = string.Join("/", one, fire, rapid, lightning, bow, crossbow, bomb, summon);
        // Stop owned delayed work before replacing its descriptors. Original source finalizers retain resource accounting.
        Reset();
        previousDb = db; previousScene = scene;
        previousSnapshot = snapshot.SnapshotId; previousItemCount = db.m_items.Count;
        RememberChoices(key, one, fire, rapid, lightning, bow, crossbow, bomb, summon);
        try
        {
            AppliedSnapshot = SecondaryAttackWorldApplySystem.Apply(db, snapshot, emitMissingWarnings: scene != null);
            if (scene != null) SecondaryAttackWorldApplySystem.ApplyToZNetScene(scene, snapshot, emitMissingWarnings: true);
            SecondaryAttackRuntimeWeaponRebind.RefreshLocalPlayerInventory(AppliedSnapshot);
            charactersDueFrame = UnityEngine.Time.frameCount;
            SubSummonSystem.RegisterRpc(ZRoutedRpc.instance);
            MagicSummonQualityPresetSystem.RefreshLoadedSummonLifetimeState();
            previousLifetime = settings.Get(EdgeSettings.BloodMagic.SummonLifetimeSeconds) + "/" + settings.Get(EdgeSettings.BloodMagic.LifetimeMultiplierAtSkill100);
        }
        catch (Exception error)
        {
            Reset();
            // Retry after a changed document, registry or setting, with vanilla attacks while the apply is rejected.
            previousDb = db; previousScene = scene;
            previousSnapshot = snapshot.SnapshotId; previousItemCount = db.m_items.Count;
            RememberChoices(key, one, fire, rapid, lightning, bow, crossbow, bomb, summon);
            SecondaryAttackWarningLog.WarnOnce("apply:" + snapshot.SnapshotId + ":" + key,
                "Edge native apply rejected: " + error.GetType().Name + ": " + error.Message);
        }
    }

    internal static void Reset()
    {
        SecondaryAttackObjectDbStateStore.RestoreOwned();
        if (previousDb == null && previousScene == null && Applied.Count == 0) return;
        SubSummonSystem.DisposeRpc();
        SecondaryAttacksCharacterRpc.DisposeAll();
        StickyDetonatorSystem.CancelForEdge();
        EdgeNativeOwnership.Dispose();
        MagicSummonQualityPresetSystem.DisposeForEdge();
        OverheadStatusUiManager.Dispose();
        SecondaryAttackRuntimeContext.ResetForEdge();
        SneakAmbushChargeSystem.ResetForEdge();
        SneakAmbushSystem.ResetForEdge();
        MeleeBoomerangProjectileSystem.ResetForEdge();
        if (previousDb != null)
        {
            SecondaryAttackObjectDbStateStore.Restore(previousDb);
            NativeSecondaryAttackCompat.PrepareForApply(previousDb);
            MagicSummonQualityPresetSystem.RestoreObjectDb(previousDb);
        }
        if (previousScene != null)
            SummonPrefabOverrideSystem.Apply(previousScene, SecondaryAttackCompiledSnapshot.Empty.MagicSummons, false);
        AppliedSnapshot = SecondaryAttackAppliedWorldSnapshot.Empty;
        SecondaryAttackRuntimeWeaponRebind.RefreshLocalPlayerInventory(AppliedSnapshot);
        previousDb = null; previousScene = null; previousSnapshot = previousItemCount = -1; previousChoices = ""; haveChoices = false; charactersDueFrame = -1;
    }

    static void RememberChoices(string key, RangedPreset one, RangedPreset fire, RangedPreset rapid, RangedPreset lightning,
        RangedPreset bow, RangedPreset crossbow, BombPreset bomb, SummonQualityPreset summon)
    {
        previousChoices = key;
        haveChoices = true;
        prevOne = one; prevFire = fire; prevRapid = rapid; prevLightning = lightning;
        prevBow = bow; prevCrossbow = crossbow; prevBomb = bomb; prevSummon = summon;
    }

    internal static void Dispose() { SummonQualityHudSystem.Dispose(); Reset(); SubSummonSystem.DisposeRpc(); SecondaryAttacksCharacterRpc.DisposeAll(); EdgeNativeOwnership.Dispose(); HunterLegacyCompat.Dispose(); }
}
