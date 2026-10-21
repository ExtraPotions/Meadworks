// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExtraPotions.Larder.Cooking
{
    public enum StationAddDecision { New, Accepted, Rejected, Stale }

    public sealed class StationAddReceipts
    {
        public const int Capacity = 256;
        const int MaxSerializedLength = 1 + Capacity * 43;
        readonly SortedDictionary<long, Receipt> receipts = new SortedDictionary<long, Receipt>();

        public int Count => receipts.Count;

        public StationAddDecision Check(long character, long sequence)
        {
            if (character == 0 || sequence <= 0) return StationAddDecision.Stale;
            if (!receipts.TryGetValue(character, out var receipt))
                return receipts.Count < Capacity ? StationAddDecision.New : StationAddDecision.Rejected;
            if (sequence < receipt.Sequence) return StationAddDecision.Stale;
            if (sequence > receipt.Sequence) return StationAddDecision.New;
            return receipt.Accepted ? StationAddDecision.Accepted : StationAddDecision.Rejected;
        }

        // The caller permits one outstanding escrow per character, so an advanced sequence retires its predecessor.
        public string Record(long character, long sequence, bool accepted)
        {
            var decision = Check(character, sequence);
            if (decision == StationAddDecision.New)
                receipts[character] = new Receipt(sequence, accepted);
            else if (!receipts.TryGetValue(character, out var receipt) || receipt.Sequence != sequence || receipt.Accepted != accepted)
                throw new InvalidOperationException("Only a new station-add request or its unchanged receipt can be recorded.");
            return Save();
        }

        public static StationAddReceipts Parse(string? serialized)
        {
            var ledger = new StationAddReceipts();
            if (string.IsNullOrEmpty(serialized)) return ledger;
            if (serialized.Length > MaxSerializedLength) throw InvalidData();
            var lines = serialized.Split('\n');
            if (lines[0] != "1" || lines.Length > Capacity + 1) throw InvalidData();
            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(':');
                if (fields.Length != 3 || !TryCharacter(fields[0], out long character)
                    || !TryPositive(fields[1], out long sequence) || (fields[2] != "0" && fields[2] != "1")
                    || ledger.receipts.ContainsKey(character)) throw InvalidData();
                ledger.receipts.Add(character, new Receipt(sequence, fields[2] == "1"));
            }
            return ledger;
        }

        string Save()
        {
            var serialized = new StringBuilder("1");
            foreach (var entry in receipts)
                serialized.Append('\n').Append(entry.Key.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(entry.Value.Sequence.ToString(CultureInfo.InvariantCulture)).Append(':').Append(entry.Value.Accepted ? '1' : '0');
            return serialized.ToString();
        }

        static bool TryPositive(string value, out long parsed)
        {
            return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed)
                && parsed > 0 && value == parsed.ToString(CultureInfo.InvariantCulture);
        }

        static bool TryCharacter(string value, out long parsed)
        {
            return long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed)
                && parsed != 0 && value == parsed.ToString(CultureInfo.InvariantCulture);
        }

        static FormatException InvalidData() => new FormatException("Invalid station-add receipt ledger.");

        readonly struct Receipt
        {
            public readonly long Sequence;
            public readonly bool Accepted;
            public Receipt(long sequence, bool accepted) { Sequence = sequence; Accepted = accepted; }
        }
    }
}
