// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: one owned, cancellable request over the existing admin relay; no automatic retry.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Commands;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingPanelJobs
    {
        static Task<OperationResult<IReadOnlyList<string>>>? pending;
        static CancellationTokenSource? cancel;
        static Action<IReadOnlyList<string>>? receive;
        static Action? failed;
        static long generation;
        internal static bool Busy => pending != null;
        internal static bool Start(string[] args, Action<IReadOnlyList<string>> callback, string operationId = RemoteAdmin.OperationId, Action? failure = null)
        {
            if (Busy || !ThingPanel.CanAdmin && !(ThingPanel.CanConfigure && args.Length > 0 && (args[0] == "config" || args[0] == "set"))) return false;
            // Validate the exact relay bounds before enqueueing so a long field cannot produce a partial command.
            try { RemoteAdmin.EncodeRequest(args); } catch { ThingPanel.Message(ThingPanel.Ui("invalid")); return false; }
            cancel = new CancellationTokenSource(); generation = CruciblePlugin.Session.Generation; receive = callback; failed = failure;
            try { pending = CruciblePlugin.RequestClient.InvokeAsync<IReadOnlyList<string>, IReadOnlyList<string>>(operationId, args, cancel.Token); }
            catch { Reset(); ThingPanel.Message(ThingPanel.Ui("request_failed")); return false; }
            ThingPanel.Message(ThingPanel.Ui("pending")); return true;
        }
        internal static void Tick()
        {
            if (pending == null) return;
            if (!ThingPanel.CanOpen || generation != CruciblePlugin.Session.Generation) { Reset(); return; }
            if (!pending.IsCompleted) return;
            var task = pending; var callback = receive; var failure = failed; pending = null; receive = null; failed = null; cancel?.Dispose(); cancel = null;
            if (task.Status != TaskStatus.RanToCompletion) { var ignored = task.Exception; if (failure != null) failure(); else ThingPanel.Message(ThingPanel.Ui("request_failed")); return; }
            var result = task.Result;
            if (result.State == OperationState.Succeeded) callback?.Invoke(result.Reply);
            else if (failure != null) failure();
            else ThingPanel.Message(ThingText.Format(RemoteAdmin.TextKey(result.Code), result.Code));
        }
        internal static void Reset()
        {
            cancel?.Cancel(); cancel?.Dispose(); cancel = null;
            var abandoned = pending; pending = null; receive = null; failed = null;
            if (abandoned != null) abandoned.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
