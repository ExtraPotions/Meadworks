// Wildblood. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// The plugin owner drives existing adopted helpers; this owns no rule, spawn or transport algorithm.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.AdoptedLuck;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Luck;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static class NativeEngineLifecycle
{
    static long generation = -1;
    static bool modifiers, pressure, attributed, bosses;
    static int characterCursor;
    static readonly HashSet<string> warned = new(StringComparer.Ordinal);

    static void Run(string part, Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            if (warned.Add(part)) Plugin.NativeLogger?.LogWarning("Wildblood native " + part + ": " + error.GetType().Name);
        }
    }

    // No closure per call; Tick runs every frame.
    static void Run<T>(string part, Action<T> action, T argument)
    {
        try { action(argument); }
        catch (Exception error)
        {
            if (warned.Add(part)) Plugin.NativeLogger?.LogWarning("Wildblood native " + part + ": " + error.GetType().Name);
        }
    }

    // A transport this owner still holds needs no registration attempt; the attempt would find the same lease and return.
    // Asking first skips the closures and name arrays every attempt allocates, every frame.
    static bool OwnsRouted(NativeRpcOwner owner)
    {
        var bus = ZRoutedRpc.instance;
        return bus != null && NativeRpcOwnership.OwnsRouted(bus, owner);
    }
    static void BossCreditTransport() { if (!OwnsRouted(NativeRpcOwner.BossCredit)) Adopted.Tally.BossCredit.EnsureRegistered(); }
    static void BossBoardTransport() { if (!OwnsRouted(NativeRpcOwner.BossBoard)) Adopted.Tally.BossBoardRpc.EnsureRegistered(); }
    static void BossRecallTransport() { if (!OwnsRouted(NativeRpcOwner.BossRecall)) Adopted.Tally.BossBoardRecall.EnsureRegistered(); }
    static void BossHintTransport() { if (!OwnsRouted(NativeRpcOwner.BossHints)) Adopted.Hints.BossHintRpc.EnsureRegistered(); }
    static void RegisterCharacter(Character character)
    {
        if (!NativeRpcOwnership.OwnsView(character.m_nview, NativeRpcOwner.CMModifierCharacter)) CreatureModifierManager.RegisterCharacterRpcs(character);
    }

    internal static void Tick()
    {
        var session = CruciblePlugin.Session;
        if (generation != session.Generation)
        {
            ResetWorld();
            generation = session.Generation;
        }
        Run("loaded crossbow lifecycle", LoadedWeapons.Tick);
        Run("summon payment recovery", NativeSummonPayment.Tick);
        bool ready = Plugin.NativeRulesReady && NativeModifierRules.Ready && session.AuthorityReady
            && session.WorldId != 0 && ZNet.instance != null && ZNet.m_world != null
            && ZNet.instance.GetWorldUID() == session.WorldId;
        Run("boss tamed pressure", NativeTamedPressure.Tick);
        if (ready) Run("boss progression settlement", BossProgress.Tick);
        if (ready) Run("abandoned summoned bosses", NativeBossAbandon.Tick);
        if (ready) Run("summoned offering delivery", NativeSummonDelivery.Tick);
        bool nextModifiers = ready && WildbloodModules.Active(WildbloodModule.Traits);
        bool nextPressure = ready && WildbloodModules.Active(WildbloodModule.Pressure);
        bool nextAttributed = StorageManager.CurrentMode == LuckMode.Attributed;
        bool nextBosses = ready && WildbloodModules.Active(WildbloodModule.Bosses);
        if (bosses && !nextBosses)
        {
            foreach (var owner in BossOwners) Run("boss transport release " + owner, () => NativeRpcOwnership.Release(owner));
            Run("boss board clear", Adopted.Tally.BossBoard.Forget);
        }
        bosses = nextBosses;
        if (bosses)
        {
            Run("boss credit transport", BossCreditTransport);
            Run("boss board transport", BossBoardTransport);
            Run("boss recall transport", BossRecallTransport);
            Run("boss hint transport", BossHintTransport);
            Run("boss tally settlement", () => Adopted.Tally.DamageTally.FlushDue(Time.time));
        }
        if (modifiers && !nextModifiers)
        {
            Run("modifier effects release", () => NativeRpcOwnership.Release(NativeRpcOwner.CMModifierEffects));
            Run("modifier characters release", () => NativeRpcOwnership.Release(NativeRpcOwner.CMModifierCharacter));
        }
        if (pressure && !nextPressure)
        {
            // The loaded ledger survives module off; Save owns its separate settlement-authority check.
            Run("pressure save", NativePressurePersistence.Save);
            Run("pressure release", () => NativeRpcOwnership.Release(NativeRpcOwner.CMKarma));
        }
        if (attributed && !nextAttributed) Run("Luck release", LuckAttributionRuntime.Reset);
        modifiers = nextModifiers; pressure = nextPressure; attributed = nextAttributed;
        if (modifiers)
        {
            Run("modifier transport", CreatureModifierManager.RegisterRpcs);
            Run("modifier character transport", RegisterCharacters);
        }
        else characterCursor = 0;
        if (pressure)
        {
            // Load the current world's retained state before any periodic pressure producer runs.
            Run("pressure load", NativePressurePersistence.Tick);
            Run("pressure transport", CreatureKarmaManager.RegisterRpcs);
            Run("pressure summons", CreatureKarmaManager.UpdateSummons);
        }
        if (attributed) Run("Luck attribution", LuckAttributionRuntime.Tick);
    }

    static void RegisterCharacters()
    {
        var characters = Character.GetAllCharacters();
        if (characters.Count == 0) { characterCursor = 0; return; }
        int count = Math.Min(32, characters.Count);
        for (int i = 0; i < count; i++)
        {
            if (characterCursor >= characters.Count) characterCursor = 0;
            Character character = characters[characterCursor++];
            if (character == null || character.m_nview == null || !character.m_nview.IsValid()) continue;
            // The existing helper and lease are idempotent for this exact native view/delegate table.
            Run("character registration", RegisterCharacter, character);
        }
    }

    static readonly NativeRpcOwner[] BossOwners = { NativeRpcOwner.BossCredit, NativeRpcOwner.BossBoard, NativeRpcOwner.BossRecall, NativeRpcOwner.BossHints };

    static void ResetWorld()
    {
        NativeTamedPressure.Reset();
        NativeBossAbandon.Reset();
        NativeSummonDelivery.Reset();
        LoadedWeapons.Reset();
        Run("AI targeting reset", CreatureTargeting.ResetRuntimeState);
        Adopted.Horde.WildSpawn.End(false);
        Run("boss progression reset", BossProgress.Reset);
        Run("boss reward reset", BossMaterialRewards.Reset);
        foreach (var owner in BossOwners) Run("boss transport reset " + owner, () => NativeRpcOwnership.Release(owner));
        Run("boss board reset", Adopted.Tally.BossBoard.Forget);
        Run("boss tally reset", Adopted.Tally.DamageTally.Reset);
        Run("Luck reset", LuckAttributionRuntime.Reset);
        foreach (var owner in new[] { NativeRpcOwner.CMModifierEffects, NativeRpcOwner.CMModifierCharacter, NativeRpcOwner.CMKarma })
            Run("transport reset " + owner, () => NativeRpcOwnership.Release(owner));
        Run("modifier reset", CreatureModifierManager.ResetRuntimeState);
        Run("spawn reset", CreatureManagerSpawnLifecycle.ResetRuntimeState);
        Run("blocker reset", CreatureSpawnBlocker.ResetRuntimeState);
        modifiers = pressure = attributed = bosses = false;
        characterCursor = 0;
    }

    internal static void SavePersonal()
    {
        Run("Luck storage settlement", () =>
        {
            if (Player.m_localPlayer != null && ZNet.instance != null && ZNet.m_world != null)
                StorageManager.SaveToCustomData(Player.m_localPlayer, ZNet.instance.GetWorldUID());
        });
    }

    internal static void Shutdown()
    {
        // Ready is already false: settlement restores only layers the exact controller still owns.
        Run("controller enumeration", () =>
        {
            foreach (Character character in Character.GetAllCharacters().ToArray())
                if (character != null && character.GetComponent<EliteController>() is EliteController controller)
                    Run("controller settlement", controller.SuspendForShutdown);
        });
        Run("altar enumeration", () =>
        {
            foreach (var altar in UnityEngine.Object.FindObjectsByType<Adopted.Aspects.AltarAspect>(FindObjectsSortMode.None))
                if (altar != null) Run("altar settlement", altar.SuspendForShutdown);
        });
        // Native Player.Load and pressure Tick may have loaded the new generation before this owner Tick.
        // World cache reset never clears those stores. Native Save/Shutdown hooks settle the old world;
        // only final plugin teardown releases the remaining loaded store after the plugin Save attempt.
        Run("Luck storage reset", StorageManager.Clear);
        Run("pressure reset", CreatureKarmaManager.ResetRuntimeState);
        Run("pressure storage reset", NativePressurePersistence.Reset);
        ResetWorld();
        generation = -1;
        warned.Clear();
    }
}
