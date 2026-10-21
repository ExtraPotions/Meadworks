// Wildblood. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT, from SaveYourLuckBruh 1.0.2.
// Pin ece802da9f806f3ee9d6e50552beec479abae4ce. MIT notice: THIRD_PARTY_NOTICES/SaveYourLuckBruh-MIT.txt.
// Changed by expDARE, 2026: Wildblood lifecycle, existing policy/codec and owned player/world persistence.
// Adapted from Core/StorageManager.cs: source player/world load, save, empty clear and ensure-loaded lifecycle.
// Existing LuckCodec owns the source format/import; attributed tables never read the process-local dictionary.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Luck;
using ExtraPotions.Wildblood.Runtime;

namespace ExtraPotions.Wildblood.AdoptedLuck;

// Staged only. The activation owner installs the four Player patches explicitly and calls Clear at teardown.
internal static class StorageManager
{
    private static Player? loadedPlayer;
    private static long loadedWorld, loadedPlayerId;
    private static LuckMode loadedMode;
    private static LuckTable counters = new LuckTable();
    private static LuckTable nativeNames = new LuckTable();
    private static bool nativeNamesWritable;
    private static bool ownsMachineCounters;
    private static Dictionary<int, int>? nativeCounters, machineCounters;
    private static bool nativeWritable;
    internal static long Revision { get; private set; }
    private static bool writable;
    private static bool hasLoggedNullCustomDataError;

    internal static bool IsLoaded => loadedPlayer != null;
    internal static bool IsLoadedFor(Player player) => ReferenceEquals(loadedPlayer, player);
    internal static string GetStorageKey(long worldUid) => LuckCodec.StorageKey(worldUid);
    internal static string GetNativeStorageKey(long worldUid) => "wildblood.luck.native." + worldUid.ToString(CultureInfo.InvariantCulture);
    internal static string GetNativeNamesKey(long worldUid) => "wildblood.luck.native.names." + worldUid.ToString(CultureInfo.InvariantCulture);

    internal static LuckMode CurrentMode
    {
        get
        {
            var settings = Plugin.Settings;
            // Wait for the game's modifier state instead of treating an unready world as pseudo-enabled.
            if (settings == null || ZoneSystem.instance == null) return LuckMode.Off;
            return LuckPolicy.Mode(WildbloodModules.Active(WildbloodModule.Luck),
                settings.Get(WildbloodSettings.Luck.PersistCounters),
                settings.Get(WildbloodSettings.Luck.AttributeToKiller),
                ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoPseudoDrops),
                WildbloodModules.ProductActive(out _));
        }
    }

    internal static bool IsPersistenceEnabled => CurrentMode == LuckMode.LocalPersisted || CurrentMode == LuckMode.Attributed;

    private static bool LocalWorld(Player player, long worldUid) => player != null &&
        player == Player.m_localPlayer && ZNet.instance != null && ZNet.m_world != null &&
        ZNet.instance.GetWorldUID() == worldUid;

    internal static void LoadFromCustomData(Player player, long worldUid)
    {
        if (LuckDeathAdmission.Holds(player, worldUid) || LuckDeathAdmission.HasUnsettled && (!ReferenceEquals(loadedPlayer, player) || loadedWorld != worldUid)) return;
        if (!LocalWorld(player, worldUid) || !IsPersistenceEnabled || !LuckDeathAdmission.MayLoadCounter(player, worldUid)) return;
        LoadPlayerData(player, worldUid);
    }

    private static void LoadPlayerData(Player player, long worldUid, bool importLegacy = true)
    {
        if (player.m_customData == null)
        {
            bool logged = hasLoggedNullCustomDataError;
            Clear();
            if (!logged)
                Plugin.NativeLogger?.LogError("Player custom data is unavailable; luck counters were not loaded or saved.");
            hasLoggedNullCustomDataError = true;
            return;
        }
        hasLoggedNullCustomDataError = false;

        // Preserve the source import entry and the existing destination exactly; ImportOnce owns its marker policy.
        if (importLegacy) LuckCodec.ImportOnce(player.m_customData, worldUid);
        player.m_customData.TryGetValue(GetStorageKey(worldUid), out string? serializedData);
        LuckTable prepared = LuckCodec.Parse(serializedData);
        bool canWrite = CanPreserveFormat(serializedData);

        Clear();
        loadedPlayer = player;
        loadedPlayerId = player.GetPlayerID();
        loadedWorld = worldUid;
        loadedMode = CurrentMode;
        counters = prepared;
        writable = canWrite;
        if (!writable)
            Plugin.NativeLogger?.LogWarning("Luck data contains unsupported or malformed entries; original character data is retained and writes are refused.");

        if (loadedMode == LuckMode.LocalPersisted)
        {
            LoadMachineCounters(player);
        }
        Publish(player);
    }

    // Parse still skips malformed rows as the source does. A later save must not destroy unknown/newer rows.
    internal static bool CanPreserveFormat(string? text)
    {
        return LuckCodec.CanPreserveFormat(text) &&
            NativeLuckCounters.TryConvert(LuckCodec.Parse(text), p => p.GetStableHashCode(), out _);
    }

    internal static void EnsureLoaded(Player player, long worldUid)
    {
        if (LuckDeathAdmission.HasUnsettled && loadedPlayer != null && (!ReferenceEquals(loadedPlayer, player) || loadedWorld != worldUid)) return;
        if (!LocalWorld(player, worldUid) || !IsPersistenceEnabled) return;
        if (!ReferenceEquals(loadedPlayer, player) || loadedWorld != worldUid)
        {
            LoadFromCustomData(player, worldUid);
            return;
        }
        if (loadedMode != CurrentMode)
        {
            // Preserve the personal table on mode changes; only local mode owns the machine dictionary.
            if (ownsMachineCounters)
            {
                // Attribution has just become active: never adopt the machine dictionary as killer data.
                // The last loaded/saved personal table remains the attribution baseline.
                WriteMachineCounters(player);
                ReleaseMachineCounters();
            }
            loadedMode = CurrentMode;
            if (loadedMode == LuckMode.LocalPersisted)
            {
                LoadMachineCounters(player);
            }
        }
        Publish(player);
    }

    private static void LoadMachineCounters(Player player)
    {
        bool opaque = player.m_customData.TryGetValue(GetNativeStorageKey(loadedWorld), out string text);
        if (opaque)
            nativeWritable = NativeLuckCounters.TryParse(text, out machineCounters);
        else nativeWritable = writable && NativeLuckCounters.TryConvert(counters, p => p.GetStableHashCode(), out machineCounters);
        if (!nativeWritable)
        {
            machineCounters = null;
            Plugin.NativeLogger?.LogWarning("Luck native data cannot be converted without loss; original data and native counters are retained.");
            return;
        }
        nativeCounters = CharacterDrop.s_pseudoCounter;
        CharacterDrop.s_pseudoCounter = machineCounters!;
        ownsMachineCounters = true;
        nativeNames = new LuckTable();
        nativeNamesWritable = true;
        if (player.m_customData.TryGetValue(GetNativeNamesKey(loadedWorld), out string names))
        {
            nativeNamesWritable = CanPreserveFormat(names);
            if (nativeNamesWritable) nativeNames = LuckCodec.Parse(names);
        }
        else if (!opaque) nativeNames = LuckCodec.Parse(LuckCodec.Write(counters));
    }

    private static void WriteMachineCounters(Player player)
    {
        if (!nativeWritable || !ownsMachineCounters || player.m_customData == null ||
            !ReferenceEquals(CharacterDrop.s_pseudoCounter, machineCounters)) return;
        // Keep an empty version marker so old legacy rows cannot be imported again after clearing.
        player.m_customData[GetNativeStorageKey(loadedWorld)] = NativeLuckCounters.Write(machineCounters!);
        if (nativeNamesWritable && nativeNames.Count > 0)
            player.m_customData[GetNativeNamesKey(loadedWorld)] = LuckCodec.Write(nativeNames);
    }

    internal static bool TryNativeNames(out LuckTable table)
    {
        table = new LuckTable();
        if (!ownsMachineCounters || !nativeNamesWritable || machineCounters == null) return false;
        foreach (var row in nativeNames.Entries)
            if (machineCounters.TryGetValue(NativeLuckCounters.Bucket(row.Prefab.GetStableHashCode(), row.Chance), out int count))
                table.Set(row.Prefab, row.Chance, count);
        return NativeLuckCounters.TryConvert(table, p => p.GetStableHashCode(), out _);
    }

    internal static void StoreNativeNames(LuckTable table)
    {
        if (ownsMachineCounters && nativeNamesWritable)
            foreach (var row in table.Entries) nativeNames.Set(row.Prefab, row.Chance, row.Counter);
    }

    private static void ReleaseMachineCounters()
    {
        if (ownsMachineCounters && (ReferenceEquals(CharacterDrop.s_pseudoCounter, machineCounters) ||
            CharacterDropPatch.OwnsCurrentCounters && ReferenceEquals(CharacterDropPatch.ProcessCounters, machineCounters)))
            CharacterDrop.s_pseudoCounter = nativeCounters!;
        else if (CharacterDropPatch.OwnsCurrentCounters)
            CharacterDrop.s_pseudoCounter = CharacterDropPatch.ProcessCounters!;
        ownsMachineCounters = false;
        nativeCounters = machineCounters = null;
        nativeWritable = false;
        nativeNames = new LuckTable(); nativeNamesWritable = false;
    }

    internal sealed class NativeSaveScope
    {
        private readonly Dictionary<int, int> previous, native;
        private readonly long revision;
        internal NativeSaveScope(Dictionary<int, int> previous, Dictionary<int, int> native)
        { this.previous = previous; this.native = native; revision = Revision; }
        internal void Restore()
        {
            if (revision == Revision && ReferenceEquals(CharacterDrop.s_pseudoCounter, native))
                CharacterDrop.s_pseudoCounter = previous;
        }
    }

    internal static NativeSaveScope? SuspendForNativeSave()
    {
        var native = ownsMachineCounters ? nativeCounters! : CharacterDropPatch.ProcessCounters;
        if (native == null || ReferenceEquals(CharacterDrop.s_pseudoCounter, native)) return null;
        var scope = new NativeSaveScope(CharacterDrop.s_pseudoCounter, native);
        CharacterDrop.s_pseudoCounter = native;
        return scope;
    }

    private static void WriteCustomData(Player player)
    {
        if (!writable || player.m_customData == null) return;
        string key = GetStorageKey(loadedWorld);
        if (counters.Count == 0) player.m_customData.Remove(key); // source empty-table clearing semantics
        else player.m_customData[key] = LuckCodec.Write(counters);
    }

    internal static void SaveToCustomData(Player player, long worldUid)
    {
        if (!LocalWorld(player, worldUid) || !IsPersistenceEnabled) return;
        EnsureLoaded(player, worldUid);
        if (!ReferenceEquals(loadedPlayer, player) || loadedWorld != worldUid) return;
        if (loadedMode == LuckMode.LocalPersisted) WriteMachineCounters(player);
        else WriteCustomData(player);
        Publish(player);
    }

    internal static void UpdateMemoryCache(Player player, long worldUid) => SaveToCustomData(player, worldUid);

    // Killer-attributed evaluation/delta ownership uses this personal table, not s_pseudoCounter.
    internal static bool TryGetTable(Player player, long worldUid, out LuckTable table)
    {
        table = null!;
        EnsureLoaded(player, worldUid);
        if (!LocalWorld(player, worldUid) || !IsPersistenceEnabled || !writable ||
            !ReferenceEquals(loadedPlayer, player) || loadedWorld != worldUid) return false;
        table = counters;
        return true;
    }

    internal static bool TryDecisionImage(Player player, long worldUid, out string image)
    {
        image = "";
        if (!LocalWorld(player, worldUid)) return false;
        if (!ReferenceEquals(loadedPlayer, player) || loadedWorld != worldUid) EnsureLoaded(player, worldUid);
        if (!writable || !ReferenceEquals(loadedPlayer, player) || loadedWorld != worldUid) return false;
        image = LuckCodec.Write(counters);
        return System.Text.Encoding.UTF8.GetByteCount(image) <= LuckCodec.PublishBytes && CanPreserveFormat(image);
    }

    internal static bool TryRecoveryImage(Player player, long worldUid, LuckDecisionLedger ledger, LuckRetainedOperation retained, out string image)
    {
        image = "";
        if (!LocalWorld(player, worldUid) || player.m_nview == null || !player.m_nview.IsValid() || !player.m_nview.IsOwner()
            || player.m_customData == null || Game.instance == null || Game.instance.GetPlayerProfile() == null
            || Game.instance.GetPlayerProfile().GetPlayerID() != player.GetPlayerID() || player.GetPlayerID() == 0
            || !LuckDeathAdmission.Session(out long currentWorld, out _) || currentWorld != worldUid
            || CurrentMode != LuckMode.Attributed || ownsMachineCounters || !ledger.MatchesStore(worldUid, player.GetPlayerID())
            || ledger.OwnerEpoch != LuckDeathAdmission.CurrentOwnerEpoch || retained.Grant.OwnerEpoch == ledger.OwnerEpoch
            || retained.Grant.Request.World != worldUid || retained.Grant.Request.Player != player.GetPlayerID()) return false;
        if (!ledger.RetainedOperations().Any(o => o.Grant.Request.Id == retained.Grant.Request.Id
            && o.GrantDigest == retained.GrantDigest && o.ResultDigest == retained.ResultDigest)) return false;
        string characterImage = player.m_customData.TryGetValue(GetStorageKey(worldUid), out string actual) ? actual : "";
        if (retained.Result == null || (characterImage != retained.Grant.BaseImage && characterImage != retained.Result.FinalImage)) return false;
        if (!ReferenceEquals(loadedPlayer, null) && (loadedWorld != worldUid || loadedPlayerId != player.GetPlayerID()
            || loadedMode != LuckMode.Attributed)) return false;
        if (!ReferenceEquals(loadedPlayer, player) || loadedWorld != worldUid)
        {
            player.m_customData.TryGetValue(GetStorageKey(worldUid), out string? serialized);
            if (!CanPreserveFormat(serialized)) return false;
            // Only the same retained attributed scope may replace its old actor's cache; Clear still preserves every hold.
            LoadPlayerData(player, worldUid, importLegacy: false);
        }
        return TryDecisionImage(player, worldUid, out image);
    }

    internal static bool TryRecoveryChainImage(Player player, long worldUid, LuckDecisionLedger ledger, LuckCounterPlan plan, out string image)
    {
        image = "";
        if (!LocalWorld(player, worldUid) || player.m_nview == null || !player.m_nview.IsValid() || !player.m_nview.IsOwner()
            || player.m_customData == null || Game.instance == null || Game.instance.GetPlayerProfile()?.GetPlayerID() != player.GetPlayerID()
            || player.GetPlayerID() == 0 || !LuckDeathAdmission.Session(out long currentWorld, out _) || currentWorld != worldUid
            || CurrentMode != LuckMode.Attributed || ownsMachineCounters || !ledger.MatchesStore(worldUid, player.GetPlayerID())
            || ledger.OwnerEpoch != LuckDeathAdmission.CurrentOwnerEpoch || plan.World != worldUid || plan.Player != player.GetPlayerID()) return false;
        string raw = player.m_customData.TryGetValue(GetStorageKey(worldUid), out string stored) ? stored : "";
        if (!ledger.ValidateCounterPlan(plan, raw) || !CanPreserveFormat(raw)) return false;
        if (!ReferenceEquals(loadedPlayer, null) && (loadedWorld != worldUid || loadedPlayerId != player.GetPlayerID()
            || loadedMode != LuckMode.Attributed)) return false;
        if (!ReferenceEquals(loadedPlayer, null) && (!writable || !ledger.ValidateCounterPlan(plan, LuckCodec.Write(counters)))) return false;
        if (!ReferenceEquals(loadedPlayer, player) || loadedWorld != worldUid)
            LoadPlayerData(player, worldUid, importLegacy: false);
        if (!TryDecisionImage(player, worldUid, out image)) return false;
        return ledger.ValidateCounterPlan(plan, image);
    }

    internal static bool CommitDecision(Player player, long worldUid, string before, string after)
    {
        if (!TryDecisionImage(player, worldUid, out string current) || (current != before && current != after)
            || !CanPreserveFormat(after) || System.Text.Encoding.UTF8.GetByteCount(after) > LuckCodec.PublishBytes
            || player.m_nview == null || !player.m_nview.IsValid() || !player.m_nview.IsOwner()) return false;
        counters = LuckCodec.Parse(after);
        WriteCustomData(player);
        Publish(player, true);
        return LuckCodec.Write(counters) == after && (player.m_customData.TryGetValue(GetStorageKey(worldUid), out string saved) ? saved : "") == after;
    }

    private static void Publish(Player player, bool decision = false)
    {
        if ((!decision && (loadedMode != LuckMode.Attributed || CurrentMode != LuckMode.Attributed)) || !writable) return;
        ZNetView? view = player.m_nview;
        if (view == null || !view.IsValid() || !view.IsOwner()) return;
        ZDO? zdo = view.GetZDO();
        if (zdo != null)
        {
            zdo.Set(LuckCodec.Prefix, LuckCodec.Publish(counters, LuckCodec.PublishBytes));
            zdo.Set(LuckAttributionRuntime.WorldKey, loadedWorld);
            zdo.Set(LuckAttributionRuntime.PityProtocolKey, 1);
            zdo.Set(LuckDeathAdmission.ProtocolKey, 1);
        }
    }

    internal static void Clear()
    {
        if (LuckDeathAdmission.HasUnsettled) return;
        ReleaseMachineCounters();
        Revision++;
        loadedPlayer = null;
        loadedWorld = loadedPlayerId = 0;
        loadedMode = LuckMode.Off;
        counters = new LuckTable();
        writable = false;
        ownsMachineCounters = false;
        hasLoggedNullCustomDataError = false;
    }
}
