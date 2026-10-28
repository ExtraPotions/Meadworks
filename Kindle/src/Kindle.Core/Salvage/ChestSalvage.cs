// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Kindle.Salvage
{
    /// <summary>
    /// The answers of Burrow's <c>meadworks.storage.chest_take</c> 1.0 (Burrow's <c>ChestItemTake</c>). Kindle has no reference
    /// to Burrow; the values are the contract.
    /// </summary>
    public static class ChestTakeCodes
    {
        public const int Taken = 0, Pending = 1, Changed = 2, Unavailable = 3;
    }

    /// <summary>What Kindle does with one answer of the chest take.</summary>
    public enum ChestTakeStep
    {
        /// <summary>The item left the chest and the materials were delivered.</summary>
        Done,
        /// <summary>Ask again next frame; nothing changed yet.</summary>
        Retry,
        /// <summary>Stop: the chest slot changed. Nothing was taken.</summary>
        Changed,
        /// <summary>Stop: the chest cannot be used now, or the wait ran out. Nothing was taken.</summary>
        Busy,
    }

    /// <summary>
    /// One salvage of a chest item waiting for Burrow: the chest's owner may need a moment to hand it over, so a pending
    /// answer is asked again until <see cref="WaitSeconds"/> pass. Delivery happens inside Burrow's take, at most once.
    /// </summary>
    public sealed class ChestTakeRetry
    {
        /// <summary>How long a salvage waits for a chest handover (Burrow asks the owner once a second).</summary>
        public const double WaitSeconds = 5;
        private readonly double deadline;

        public ChestTakeRetry(double now) { deadline = now + WaitSeconds; }

        /// <summary>
        /// The step for an answer at <paramref name="now"/>. Burrow delivers only after the removal is decided, so a delivery
        /// is done whatever code follows it; a Taken answer without a delivery never counts as done.
        /// </summary>
        public ChestTakeStep Next(int code, bool delivered, double now)
        {
            if (delivered)
                return ChestTakeStep.Done;
            if (code == ChestTakeCodes.Taken)
                return ChestTakeStep.Busy;
            if (code == ChestTakeCodes.Pending)
                return now < deadline ? ChestTakeStep.Retry : ChestTakeStep.Busy;
            return code == ChestTakeCodes.Changed ? ChestTakeStep.Changed : ChestTakeStep.Busy;
        }
    }

    /// <summary>
    /// Salvaged materials go into the player's inventory; whatever it refuses drops at the player's feet, so nothing is lost
    /// and the delivery cannot fail half way (user decision 2026-10-21).
    /// </summary>
    public static class SalvageDelivery
    {
        /// <summary>
        /// Adds each material in stacks of at most its stack size with <paramref name="add"/> (which answers how many units
        /// landed) and drops every unit that did not land with <paramref name="drop"/>. When <paramref name="add"/> throws,
        /// <paramref name="stock"/> is the count of that material before and after, and only the increase counts as landed.
        /// Without <paramref name="stock"/>, a throw still counts as nothing landed. Returns the units dropped.
        /// </summary>
        public static int Deliver<T>(IEnumerable<(T Material, int Amount, int MaxStack)> returns, Func<T, int, int> add, Action<T, int> drop, Func<T, int>? stock = null)
        {
            List<(T Material, int Amount, int MaxStack)> outstanding = DeliverConfirmed(returns, add, (material, amount) => { drop(material, amount); return true; }, out int dropped, stock);
            if (outstanding.Count != 0) throw new InvalidOperationException("A void drop callback cannot leave salvage outstanding.");
            return dropped;
        }

        public static List<(T Material, int Amount, int MaxStack)> DeliverConfirmed<T>(IEnumerable<(T Material, int Amount, int MaxStack)> returns, Func<T, int, int> add, Func<T, int, bool> drop, out int dropped, Func<T, int>? stock = null)
        {
            return DeliverConfirmed(returns, add, drop, out dropped, stock, false);
        }

        public static List<(T Material, int Amount, int MaxStack)> DeliverConfirmed<T>(IEnumerable<(T Material, int Amount, int MaxStack)> returns, Func<T, int, int> add, Func<T, int, bool> drop, out int dropped, Func<T, int>? stock, bool dropExceptionMayHaveDelivered)
        {
            if (returns == null) throw new ArgumentNullException(nameof(returns));
            if (add == null) throw new ArgumentNullException(nameof(add));
            if (drop == null) throw new ArgumentNullException(nameof(drop));
            dropped = 0;
            var outstanding = new List<(T Material, int Amount, int MaxStack)>();
            foreach (var (material, amount, maxStack) in returns)
            {
                int size = Math.Max(1, maxStack);
                for (int left = amount; left > 0;)
                {
                    int chunk = Math.Min(size, left);
                    int before = ReadStock(stock, material);
                    int landed;
                    try { landed = Math.Max(0, Math.Min(chunk, add(material, chunk))); }
                    catch (Exception)
                    {
                        int after = ReadStock(stock, material);
                        landed = stock == null ? 0 : Math.Max(0, Math.Min(chunk, after - before));
                    }
                    if (landed < chunk)
                    {
                        int overflow = chunk - landed;
                        bool confirmed;
                        try { confirmed = drop(material, overflow); }
                        catch (Exception) { confirmed = dropExceptionMayHaveDelivered; }
                        if (confirmed) dropped += overflow;
                        else outstanding.Add((material, overflow, maxStack));
                    }
                    left -= chunk;
                }
            }
            return outstanding;
        }

        static int ReadStock<T>(Func<T, int>? stock, T material)
        {
            if (stock == null) return 0;
            try { return Math.Max(0, stock(material)); }
            catch (Exception) { return 0; }
        }
    }

    public static class SalvageRecovery
    {
        public static string Encode(IEnumerable<(string Prefab, int Amount)> entries)
        {
            var text = new StringBuilder("1");
            foreach (var (prefab, amount) in entries)
            {
                if (string.IsNullOrEmpty(prefab) || amount <= 0) continue;
                text.Append(';').Append(amount.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(prefab)));
            }
            return text.ToString();
        }

        public static bool TryDecode(string text, out List<(string Prefab, int Amount)> entries)
        {
            entries = new List<(string Prefab, int Amount)>();
            if (string.IsNullOrEmpty(text) || text.Length > 65536) return false;
            string[] parts = text.Split(';');
            if (parts.Length == 0 || parts[0] != "1") return false;
            try
            {
                for (int i = 1; i < parts.Length; i++)
                {
                    int split = parts[i].IndexOf(':');
                    if (split <= 0 || !int.TryParse(parts[i].Substring(0, split), NumberStyles.None, CultureInfo.InvariantCulture, out int amount) || amount <= 0)
                        return false;
                    string prefab = Encoding.UTF8.GetString(Convert.FromBase64String(parts[i].Substring(split + 1)));
                    if (string.IsNullOrEmpty(prefab)) return false;
                    entries.Add((prefab, amount));
                }
                return entries.Count > 0;
            }
            catch (FormatException) { entries.Clear(); return false; }
        }
    }
}
