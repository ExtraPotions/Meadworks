// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, behavior only: AutoConnector documents adding saved servers to Favorites; the game owns its list.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Beacon.Config;
using ExtraPotions.Beacon.Servers;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconFavorites
    {
        static ServerListGui? owner;
        static readonly Dictionary<string, ServerAddress> added = new Dictionary<string, ServerAddress>();
        static readonly HashSet<ServerAddress> suppressed = new HashSet<ServerAddress>();
        static readonly Queue<RemovalState> deferred = new Queue<RemovalState>();
        static readonly HashSet<string> reportedErrors = new HashSet<string>();
        static float nextSync;

        internal sealed class RemovalState
        {
            internal RemovalState(ServerJoinData data, ServerAddress address, ServerEntry entry)
            { Data = data; Address = address; Reference = entry.SecretRef; Canonical = entry.Address; }
            internal ServerJoinData Data;
            internal ServerAddress Address, Canonical;
            internal string Reference;
        }

        internal static RemovalState? Capture(ServerListGui gui)
        {
            if (!BeaconJoin.Active || gui.m_favoriteServersList == null || gui.m_serverLists[gui.m_currentServerList] != gui.m_favoriteServersList) return null;
            var index = gui.GetSelectedServer(); var rows = gui.CurrentServerListFiltered;
            if (index < 0 || index >= rows.Count) return null;
            var data = rows[index].m_joinData;
            var address = BeaconJoin.TargetOf(data);
            var entry = address == null ? null : BeaconServers.List.Find(address);
            return entry == null || address == null ? null : new RemovalState(data, address, entry);
        }

        internal static void Removed(ServerListGui gui, RemovalState? state)
        {
            if (state == null || !BeaconJoin.Active || gui.m_favoriteServersList.Contains(state.Data)) return;
            // Even a refused metadata write must not immediately recreate a favorite the player just removed.
            suppressed.Add(state.Canonical);
            if (Plugin.Settings?.Get(BeaconSettings.General.RemoveEntryWithFavorite) != true) return;
            deferred.Enqueue(state);
        }

        public static void Tick()
        {
            if (!BeaconJoin.Active) { Reset(); return; }
            BeaconFavoriteUndo.Tick();
            if (Game.instance != null || BeaconImportSecrets.Busy || Plugin.Settings == null || Plugin.Log == null) return;
            var gui = ServerListGui.s_instance;
            if (gui == null || gui.m_favoriteServersList == null) return;
            if (!Plugin.Settings.Get(BeaconSettings.General.RemoveEntryWithFavorite)) deferred.Clear();
            while (deferred.Count > 0)
            {
                var removal = deferred.Dequeue();
                if (gui.m_favoriteServersList.Contains(removal.Data)) { suppressed.Remove(removal.Canonical); continue; }
                var current = BeaconServers.List.Entries.FirstOrDefault(e => e.SecretRef == removal.Reference);
                if (current == null || !current.Matches(removal.Address) || current.Address != removal.Canonical) continue;
                ServerRemoval? undo = null;
                if (BeaconServers.Amend(candidate => undo = candidate.RemoveForUndo(current.Name), Plugin.Log) && undo != null)
                    BeaconFavoriteUndo.Show(undo, removal.Address);
                else BeaconFavoriteUndo.Failure(BeaconPanel.Ui("write_failed"));
            }
            if (owner != gui) { owner = gui; added.Clear(); nextSync = 0; }
            if (Time.unscaledTime < nextSync) return;
            nextSync = Time.unscaledTime + .5f;
            try { Sync(gui, Plugin.Settings.Get(BeaconSettings.General.AddToFavorites)); }
            catch (Exception error) { if (reportedErrors.Add(error.GetType().Name)) Plugin.Log.Warning("Beacon favorites synchronization: " + error.GetType().Name); }
        }

        static void Sync(ServerListGui gui, bool enabled)
        {
            var favorites = gui.m_favoriteServersList; var changed = false;
            foreach (var pair in added.ToArray())
            {
                var entry = BeaconServers.List.Entries.FirstOrDefault(e => e.SecretRef == pair.Key);
                if (enabled && entry != null && entry.Address == pair.Value) continue;
                if (BeaconJoin.TryJoinData(pair.Value, out var previous) && favorites.Contains(previous)) { favorites.Remove(previous); changed = true; }
                added.Remove(pair.Key);
            }
            if (enabled)
                foreach (var entry in BeaconServers.List.Entries)
                {
                    if (suppressed.Contains(entry.Address) || !BeaconJoin.TryJoinData(entry.Address, out var data) || favorites.Contains(data)) continue;
                    MultiBackendMatchmaking.SetServerName(data, new ServerNameAtTimePoint(entry.Name, DateTime.UtcNow));
                    favorites.Add(data); added[entry.SecretRef] = entry.Address; changed = true;
                }
            if (changed) Changed(gui);
        }

        static void Changed(ServerListGui gui)
        {
            gui.m_filteredListOutdated = true; gui.SetButtonsOutdated(); ServerListGui.UpdateServerListGuiStatic();
            var saved = gui.m_favoriteServersList.Save();
            if (saved != SaveStatusCode.Succeess && saved != SaveStatusCode.SuccessNoWriteNeeded)
                Plugin.Log?.Warning(BeaconText.Text("ui_favorite_save_failed") + " (" + saved + ")");
        }

        internal static bool Undo(ServerRemoval removal, ServerAddress favorite)
        {
            if (!BeaconJoin.Active || BeaconImportSecrets.Busy || Plugin.Log == null) return false;
            try
            {
                if (!BeaconServers.Amend(candidate => candidate.Restore(removal), Plugin.Log)) return false;
                suppressed.Remove(removal.Entry.Address);
                var gui = ServerListGui.s_instance;
                if (gui != null && gui.m_favoriteServersList != null && BeaconJoin.TryJoinData(favorite, out var data) && !gui.m_favoriteServersList.Contains(data))
                { gui.m_favoriteServersList.Add(data); Changed(gui); }
                nextSync = 0; return true;
            }
            catch (Exception error) { Plugin.Log.Warning("Beacon favorite undo refused: " + error.GetType().Name); return false; }
        }

        public static void Reset()
        { owner = null; added.Clear(); suppressed.Clear(); deferred.Clear(); reportedErrors.Clear(); nextSync = 0; BeaconFavoriteUndo.Reset(); }
    }

    [HarmonyPatch(typeof(ServerListGui), "OnRemoveServerConfirm")]
    internal static class BeaconFavoritePatches
    {
        [HarmonyPrefix]
        static void Before(ServerListGui __instance, out BeaconFavorites.RemovalState? __state)
        {
            __state = null;
            try { __state = BeaconFavorites.Capture(__instance); }
            catch (Exception error) { Plugin.Log?.Warning("Beacon favorite removal capture: " + error.GetType().Name); }
        }
        [HarmonyPostfix]
        static void After(ServerListGui __instance, BeaconFavorites.RemovalState? __state)
        {
            try { BeaconFavorites.Removed(__instance, __state); }
            catch (Exception error) { Plugin.Log?.Warning("Beacon favorite removal: " + error.GetType().Name); }
        }
    }
}
