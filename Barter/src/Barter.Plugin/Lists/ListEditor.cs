// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using ExtraPotions.Barter.Lists;
using ExtraPotions.Barter.Runtime.Lists;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Barter.Runtime
{
    internal static class ListEditor
    {
        private const string Operation = BarterOperations.EditListId;
        private static IDisposable operation;
        private static readonly List<IDisposable> files = new List<IDisposable>();
        private static string registeredRevision;
        private static Task<OperationResult<bool>> pending;
        private static Action<bool> completed;
        private static long pendingGeneration;

        internal static bool CanEdit => Plugin.Active && CruciblePlugin.IsReady && TradeLists.Ready &&
            MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.ServerAuthoritative).Allowed;

        internal static void Initialize(ProductRegistration owner)
        {
            operation = CruciblePlugin.Services.Operations.Register(owner,
                BarterOperations.EditList, new ListEditCodec(), Execute);
            TradeLists.Changed += RegisterFiles;
            RegisterFiles();
        }

        private static OperationExecution<bool> Execute(AuthorityContext context, ListEditRequest request)
        {
            if (!Plugin.Active || ZNet.instance == null || !ZNet.instance.IsServer() || !context.Session.AuthorityReady ||
                !(context.IsLocal || context.IsAdmin)) return OperationExecution<bool>.Rejected("NotAuthorized");
            try
            {
                if (!Save(request.Name, request.Revision, request.Text, context.MarkDecisionMade))
                    return OperationExecution<bool>.Rejected("StaleOrInvalidList");
                return OperationExecution<bool>.Success(true);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { Plugin.Log?.Warning("List edit refused: " + error.GetType().Name); return OperationExecution<bool>.Rejected("WriteFailed"); }
        }

        private static bool Save(string name, string revision, string text, Action decision)
        {
            // Reload from disk before comparison; stale clients cannot overwrite a hot-reloaded or externally edited list set.
            TradeLists.Reload();
            if (revision != ListEditRules.Revision(TradeLists.ReadForEdit())) return false;
            if (!TradeLists.Ready || revision != ListEditRules.Revision(TradeLists.Files) || !TradeLists.Files.TryGetValue(name, out string expected)) return false;
            if (ValidateSnapshot(name, text).Count != 0) return false;
            string path = ListEditRules.OwnedPath(Paths.ConfigPath, name);
            decision?.Invoke();
            if (!ListEditRules.AtomicReplace(path, expected, text)) return false;
            TradeLists.Reload();
            return TradeLists.Ready && TradeLists.Files.TryGetValue(name, out string actual) && actual == text;
        }

        internal static IReadOnlyList<string> ValidateSnapshot(string name, string text)
        {
            var errors = new List<string>();
            long bytes = 0;
            if (!TradeLists.Files.ContainsKey(name)) errors.Add(BarterText.Text("editor_invalid"));
            foreach (var pair in TradeLists.Files)
            {
                string content = pair.Key == name ? text : pair.Value;
                bytes += Encoding.UTF8.GetByteCount(content ?? "");
                if (!ListFileName.TryParse(pair.Key, out var parsed, pair.Key.StartsWith("Barter/", StringComparison.OrdinalIgnoreCase)))
                { errors.Add(BarterText.Text("editor_invalid")); continue; }
                var result = ListRows.Read(parsed.Format, content, pair.Key);
                errors.AddRange(result.Errors.Select(error => pair.Key + ": " + error));
                if (ObjectDB.instance != null)
                    foreach (var entry in result.Entries)
                    {
                        var item = TradeWorld.Item(entry.Prefab);
                        if (item == null || entry.Quality > item.m_itemData.m_shared.m_maxQuality ||
                            (!string.IsNullOrWhiteSpace(entry.Currency) && TradeWorld.Item(entry.Currency) == null))
                            errors.Add(BarterText.Text("editor_invalid"));
                    }
            }
            if (bytes > 4 * 1024 * 1024) errors.Add(BarterText.Text("editor_invalid"));
            return errors;
        }

        internal static bool Submit(string name, string revision, string text, Action<bool> done)
        {
            if (!CanEdit || pending != null || !ListEditCodec.FitsRequest(name, revision, text)) return false;
            try
            {
                pending = CruciblePlugin.RequestClient.InvokeAsync<ListEditRequest, bool>(Operation,
                    new ListEditRequest { Name = name, Revision = revision, Text = text }, CancellationToken.None);
            }
            catch (Exception error)
            {
                Plugin.Log?.Warning("List edit transport unavailable: " + error.GetType().Name);
                return false;
            }
            pendingGeneration = CruciblePlugin.Session.Generation;
            completed = done;
            return true;
        }

        internal static void Tick()
        {
            if (pending == null || !pending.IsCompleted) return;
            var task = pending; var callback = completed; pending = null; completed = null;
            bool success = CruciblePlugin.Session.Generation == pendingGeneration && task.Status == TaskStatus.RanToCompletion &&
                task.Result.State == OperationState.Succeeded && task.Result.Reply;
            callback?.Invoke(success);
        }

        private static void RegisterFiles()
        {
            string revision = ListEditRules.Revision(TradeLists.Files);
            if (registeredRevision == revision) return;
            registeredRevision = revision;
            foreach (var file in files) file.Dispose(); files.Clear();
            foreach (string relative in TradeLists.Files.Keys)
            {
                string name = relative;
                files.Add(MeadworksDataFiles.Register(new DataFileRegistration(Plugin.Guid, name,
                    Path.Combine(Paths.ConfigPath, name.Replace('/', Path.DirectorySeparatorChar)),
                    () => TradeLists.Files.TryGetValue(name, out string text) ? text : "",
                    text => ValidateSnapshot(name, text),
                    (old, text) => CanEdit && CruciblePlugin.Session.Role != SessionRole.RemoteClient &&
                        TradeLists.Files.TryGetValue(name, out string current) && current == old &&
                        Save(name, revision, text, null))));
            }
        }

        internal static void Shutdown()
        {
            TradeLists.Changed -= RegisterFiles;
            foreach (var file in files) file.Dispose(); files.Clear();
            operation?.Dispose(); operation = null; registeredRevision = null; pending = null; completed = null;
            ListEditorWindow.Dispose();
        }
    }
}
