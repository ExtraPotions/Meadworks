// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexSpawn
{
    static Task<OperationResult<HexSpawnReply>>? pending;
    static CancellationTokenSource? cancel;
    static HexSpawnRequest? request;
    static Player? actor;
    static ZNet? net;
    static long generation = -1, profile;
    static HexSpawnReply? delivery;
    static float until;
    static int scan, picked;
    static readonly HashSet<ZDOID> attempted = new();
    internal static bool Allowed => Plugin.Ready && Plugin.Active && Plugin.Settings?.Enabled.Value == true && Plugin.Settings.Spawning.Value
        && HexMovement.Owned(Player.m_localPlayer) && HexGrantHint.SpawnAllowed();
    internal static string Request(HexSpawnRequest value, bool confirmed = false)
    {
        if (pending != null || delivery != null) return HexText.Text("spawn_busy");
        if (!Allowed) return HexText.Text("spawn_denied");
        if (!HexSpawnData.Valid(value)) return HexText.Text("spawn_help");
        var entry = HexSpawnCatalog.Resolve(value);
        if (entry == null || !HexSpawnData.Supported(value, entry.MaxQuality, entry.Tameable)) return HexText.Text("spawn_unsupported");
        if (!confirmed && value.Amount > Plugin.Settings!.ConfirmSpawnAbove.Value) return HexText.Format("spawn_confirm", value.Amount);
        if (MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Economy).Allowed
            && !MeadworksMischief.TryUse(Plugin.Guid, "Spawn.Create", MischiefCategory.Economy, HexText.Text("label_spawning"))) return HexText.Text("spawn_denied");
        Reset(); request = value; actor = Player.m_localPlayer; net = ZNet.instance; generation = CruciblePlugin.Session.Generation; profile = actor.GetPlayerID();
        if (profile == 0 || net == null) { Reset(); return HexText.Text("spawn_denied"); }
        try
        {
            cancel = new CancellationTokenSource();
            pending = CruciblePlugin.RequestClient.InvokeAsync<HexSpawnRequest, HexSpawnReply>(HexSpawnData.OperationId, value, cancel.Token);
            return HexText.Text("spawn_waiting");
        }
        catch { Reset(); return HexText.Text("spawn_failed"); }
    }
    internal static string Execute(string[] args)
    {
        // hex spawn item <prefab> <amount> <quality> inventory|drop [confirm]
        // hex spawn creature <prefab> <amount> <level> tame|wild [confirm]
        if (args.Length < 6 || args.Length > 7 || args.Length == 7 && args[6] != "confirm"
            || !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            || !int.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out var level)) return HexText.Text("spawn_help");
        var item = args[1].Equals("item", StringComparison.OrdinalIgnoreCase);
        var creature = args[1].Equals("creature", StringComparison.OrdinalIgnoreCase);
        var option = args[5].ToLowerInvariant();
        if (!item && !creature || item && option != "inventory" && option != "drop" || creature && option != "tame" && option != "wild") return HexText.Text("spawn_help");
        return Request(new HexSpawnRequest(item ? HexSpawnKind.Item : HexSpawnKind.Creature, args[2], amount, level, option == "tame", option == "inventory"), args.Length == 7);
    }
    static bool Current => Allowed && actor != null && actor == Player.m_localPlayer && actor.GetPlayerID() == profile
        && net != null && net == ZNet.instance && generation == CruciblePlugin.Session.Generation;
    internal static void Tick()
    {
        if (pending == null && delivery == null) return;
        if (!Current) { if (delivery != null) Notice(HexText.Format("spawn_canceled", delivery.Created, picked)); Reset(); return; }
        if (pending?.IsCompleted == true)
        {
            var task = pending; pending = null; cancel?.Dispose(); cancel = null;
            try
            {
                var result = task.GetAwaiter().GetResult();
                if (result.State != OperationState.Succeeded || result.Identity == null || result.Reply == null || result.Reply.Created > request!.Amount
                    || result.Reply.Partial != (result.Reply.Created != request.Amount)) { Notice(HexText.Text("spawn_failed")); Reset(); return; }
                Notice(HexText.Format("spawn_created", result.Reply.Created, request.Amount));
                if (!request.Inventory || result.Reply.Created == 0) { Reset(); return; }
                delivery = result.Reply; until = Time.unscaledTime + 15f;
            }
            catch { Notice(HexText.Text("spawn_failed")); Reset(); return; }
        }
        if (delivery == null) return;
        // Existing native drops only: no copies, direct AddItem or recreation when streaming/pickup fails.
        var instances = ItemDrop.s_instances; var count = Math.Min(64, instances.Count); var pickups = 0;
        for (var i = 0; i < count && pickups < 4 && instances.Count > 0; i++)
        {
            if (scan >= instances.Count) scan = 0;
            var drop = instances[scan++]; var id = ZDOID.None;
            try
            {
                var view = drop != null ? drop.m_nview : null;
                if (view == null || !view.IsValid()) continue;
                var zdo = view.GetZDO(); id = zdo.m_uid;
                if (attempted.Contains(id) || zdo.GetString(HexSpawnAuthority.ReceiptField, "") != delivery.Receipt
                    || zdo.GetLong(HexSpawnAuthority.ActorField, 0) != profile) continue;
                var original = zdo.GetInt(HexSpawnAuthority.StackField, 0);
                if (original < 1 || original > delivery.Created) { attempted.Add(id); continue; }
                if (!view.IsOwner()) { drop.RequestOwn(); continue; }
                drop.Load();
                // Native auto-stacking may have absorbed this receipt or mixed in unrelated drops. Leave changed stacks alone.
                if (drop.m_itemData.m_stack != original) { attempted.Add(id); continue; }
                if (!drop.CanPickup()) continue;
                if (!Current) { Notice(HexText.Format("spawn_canceled", delivery.Created, picked)); Reset(); return; }
                attempted.Add(id); pickups++;
                // Calling ItemDrop.Pickup while unowned queues a native requester beyond our cancellation lifetime.
                // This synchronous owned path uses the same native inventory/pickup logic without queuing one.
                var complete = actor!.Pickup(drop.gameObject, autoequip: false);
                var delivered = complete ? original : drop != null ? Math.Max(0, original - drop.m_itemData.m_stack) : 0;
                picked = Math.Min(delivery.Created, picked + Math.Min(original, delivered));
                if (drop != null && view != null && view.IsValid() && view.IsOwner()) drop.Save();
            }
            catch
            {
                if (id != ZDOID.None) attempted.Add(id);
                // Native/external ownership, load or pickup faults cannot abort remaining scans or the timeout.
                // The affected native identity is never retried; any partial mutation/residual remains native.
            }
        }
        if (picked >= delivery.Created || Time.unscaledTime >= until)
        { Notice(HexText.Format("spawn_delivered", delivery.Created, picked)); Reset(); }
    }
    static void Notice(string text) { if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text); }
    internal static void Reset()
    {
        var ownedCancel = cancel; cancel = null;
        try { ownedCancel?.Cancel(); } catch { } finally { ownedCancel?.Dispose(); }
        if (pending != null) _ = pending.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        pending = null; request = null; delivery = null; actor = null; net = null; profile = 0; generation = -1;
        scan = picked = 0; until = 0; attempted.Clear();
    }
}
