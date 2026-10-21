// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: delivery and its single receipt are saved in the same character image.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingInventoryDelivery
    {
        internal const string Receipt = "expDARE.Thing.DeliveryReceipt";
        const int MaxRetired = 4096;
        static readonly HashSet<string> retired = new HashSet<string>(StringComparer.Ordinal);
        static Task<OperationResult<IReadOnlyList<string>>> claim;
        static CancellationTokenSource cancellation;
        static long generation = -1;

        internal static bool RequestClaim(string token)
        {
            Tick();
            if (!ValidToken(token) || claim != null || !Plugin.Ready || !CruciblePlugin.IsReady
                || !CruciblePlugin.Session.AuthorityReady || Player.m_localPlayer == null) return false;
            cancellation = new CancellationTokenSource();
            try
            {
                claim = CruciblePlugin.RequestClient.InvokeAsync<IReadOnlyList<string>, IReadOnlyList<string>>(
                    ThingInventory.OperationId, new[] { "claim", token }, cancellation.Token);
                return true;
            }
            catch { cancellation.Dispose(); cancellation = null; return false; }
        }

        internal static void Tick()
        {
            if (!CruciblePlugin.IsReady) { ResetClaim(); return; }
            var current = CruciblePlugin.Session.Generation;
            if (generation != current)
            {
                ResetClaim();
                retired.Clear();
                generation = current;
            }
            if (claim == null || !claim.IsCompleted) return;
            try { claim.GetAwaiter().GetResult(); } catch { }
            claim = null;
            cancellation?.Dispose(); cancellation = null;
        }

        internal static string Receive(Player player, string token, string encoded, string digest, string persistentReceiverId, ItemSlot slot)
        {
            Tick();
            if (!CruciblePlugin.IsReady || !BoundCharacter(player, persistentReceiverId) || !ValidToken(token) || !ValidDigest(digest)) return "unchanged";
            if (retired.Contains(token)) return "unchanged";
            if (!ThingInventory.ValidatePayload(encoded, slot, out var item)) return "unchanged";
            try { if (ThingInventory.Digest(encoded) != digest) return "unchanged"; }
            catch { return "unchanged"; }
            var receipt = ReceiptText(token, digest, persistentReceiverId);
            if (player.m_customData.TryGetValue(Receipt, out var prior))
                return prior == receipt && SaveCharacter(player) ? "stored" : "pending";
            if (retired.Count >= MaxRetired) return "unchanged";
            if (SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter)) return "unchanged";
            var inventory = player.GetInventory();
            if (inventory == null || inventory.FindEmptySlot(inventory.TopFirst(item)).x < 0) return "unchanged";
            var inserted = false;
            player.m_customData[Receipt] = receipt;
            try
            {
                if (!ThingInventoryPickup.PlaceDistinct(inventory, item, out inserted))
                {
                    if (!inserted) player.m_customData.Remove(Receipt);
                    return inserted ? "pending" : "unchanged";
                }
            }
            catch
            {
                // Changed callbacks may throw after insertion. Keep that receipt so retry cannot add a second stack.
                if (!inserted) player.m_customData.Remove(Receipt);
                return inserted ? "pending" : "unchanged";
            }
            return SaveCharacter(player) ? "stored" : "pending";
        }

        internal static string Forget(Player player, string token, string digest, string persistentReceiverId)
        {
            Tick();
            if (!CruciblePlugin.IsReady || !BoundCharacter(player, persistentReceiverId) || !ValidToken(token) || !ValidDigest(digest)) return "pending";
            if (!retired.Contains(token) && retired.Count >= MaxRetired) return "pending";
            if (!player.m_customData.TryGetValue(Receipt, out var prior)) { retired.Add(token); return "forgotten"; }
            if (prior != ReceiptText(token, digest, persistentReceiverId)) return "pending";
            player.m_customData.Remove(Receipt);
            if (SaveCharacter(player)) { retired.Add(token); return "forgotten"; }
            player.m_customData[Receipt] = prior;
            return "pending";
        }

        static bool BoundCharacter(Player player, string character) => player != null && Player.m_localPlayer == player
            && player.m_customData != null && ZNet.instance != null && Game.instance?.GetPlayerProfile() != null
            && player.GetPlayerID().ToString(CultureInfo.InvariantCulture) == character
            && long.TryParse(character, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var id) && id != 0;

        static string ReceiptText(string token, string digest, string character) =>
            ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture) + ":" + character + ":" + token + ":" + digest;

        static bool SaveCharacter(Player player)
        {
            try
            {
                var profile = Game.instance?.GetPlayerProfile();
                if (profile == null || Player.m_localPlayer != player || SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter)) return false;
                profile.SavePlayerData(player);
                return profile.Save();
            }
            catch { return false; }
        }

        static bool ValidToken(string token) => token != null && token.Length == 32 && token.All(Hex);
        static bool ValidDigest(string digest) => digest != null && digest.Length == 64 && digest.All(Hex);
        static bool Hex(char value) => value >= '0' && value <= '9' || value >= 'a' && value <= 'f';

        static void ResetClaim()
        {
            cancellation?.Cancel();
            if (claim != null)
                claim.ContinueWith(task => { var observed = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            claim = null;
            cancellation?.Dispose(); cancellation = null;
        }

        internal static void Shutdown() => ResetClaim();
    }
}
