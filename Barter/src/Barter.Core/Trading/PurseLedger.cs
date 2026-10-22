// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;

namespace ExtraPotions.Barter.Trading
{
    /// <summary>A single unresolved trade locks a purse until its inventory outcome is known.</summary>
    public sealed class PurseLedger
    {
        public int Balance { get; private set; }
        public string Actor { get; private set; } = "";
        public string Token { get; private set; } = "";
        public string Epoch { get; private set; } = "";
        public string Fingerprint { get; private set; } = "";
        public long Delta { get; private set; }
        public bool Pending { get; private set; }
        public bool Committed { get; private set; }
        public bool Quarantined { get; private set; }
        public long ReservedAt { get; private set; }

        public PurseLedger(int initial) { Balance = Math.Max(initial, 0); }

        public bool Reserve(string actor, string token, string epoch, string fingerprint, long delta, out string reason)
        {
            reason = "InvalidReservation";
            if (string.IsNullOrEmpty(actor) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(epoch) ||
                string.IsNullOrEmpty(fingerprint) || delta < -int.MaxValue || delta > int.MaxValue)
                return false;
            if (Actor == actor && Token == token && Epoch == epoch)
            {
                reason = Pending && Fingerprint == fingerprint && Delta == delta ? "Reserved" : "AlreadyFinished";
                return Pending && Fingerprint == fingerprint && Delta == delta;
            }
            if (Pending) { reason = "TraderBusy"; return false; }
            if ((long)Balance + Math.Min(delta, 0) < 0) { reason = "TraderPoor"; return false; }
            if ((long)Balance + Math.Max(delta, 0) > int.MaxValue) { reason = "PurseOverflow"; return false; }
            Actor = actor; Token = token; Epoch = epoch; Fingerprint = fingerprint; Delta = delta;
            Balance += (int)Math.Min(delta, 0);
            Pending = true; Committed = false; Quarantined = false;
            ReservedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); reason = "Reserved";
            return true;
        }

        public bool Finish(string actor, string token, string epoch, bool committed, bool inventoryVerified = false)
        {
            if (!inventoryVerified || Quarantined || Actor != actor || Token != token || Epoch != epoch) return false;
            if (!Pending) return Committed == committed;
            Settle(committed);
            return true;
        }

        // Cooperative peers are trusted for purse outcomes, never for canonical item escrow.
        public bool FinishReported(string actor, string token, string epoch, bool committed)
        {
            if (Quarantined || Actor != actor || Token != token || Epoch != epoch) return false;
            // Legacy terminal reports already closed the ledger; never settle them retroactively.
            if (Pending) Settle(committed);
            return true;
        }

        private void Settle(bool committed)
        {
            Balance += (int)(committed ? Math.Max(Delta, 0) : -Math.Min(Delta, 0));
            Pending = false; Committed = committed;
        }

        // Unknown outcomes release serialization only. Reserved money is never refunded or credited by timeout.
        public bool QuarantineExpired(long now, int timeoutSeconds = 120)
        {
            if (!Pending || timeoutSeconds < 1 || now < ReservedAt || now - ReservedAt < timeoutSeconds) return false;
            Pending = false; Quarantined = true;
            return true;
        }

        public bool Refill(int next)
        {
            if (Pending || next < 0) return false;
            Balance = next;
            return true;
        }

        // Balance and pending state travel in one ZDO value; the legacy balance is only a mirror.
        public string Encode()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(2); writer.Write(Balance); writer.Write(Actor); writer.Write(Token); writer.Write(Epoch);
                writer.Write(Fingerprint); writer.Write(Delta); writer.Write(Pending); writer.Write(Committed);
                writer.Write(Quarantined); writer.Write(ReservedAt);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        public static PurseLedger Decode(string encoded)
        {
            if (encoded == null || encoded.Length > 4096) throw new InvalidDataException("Ledger exceeds bounds.");
            using (var stream = new MemoryStream(Convert.FromBase64String(encoded)))
            using (var reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                if (version != 1 && version != 2) throw new InvalidDataException("Unsupported ledger version.");
                int balance = reader.ReadInt32();
                var ledger = new PurseLedger(balance) { Actor = reader.ReadString(), Token = reader.ReadString(),
                    Epoch = reader.ReadString(), Fingerprint = reader.ReadString(), Delta = reader.ReadInt64(),
                    Pending = reader.ReadBoolean(), Committed = reader.ReadBoolean() };
                if (version == 2) { ledger.Quarantined = reader.ReadBoolean(); ledger.ReservedAt = reader.ReadInt64(); }
                if (balance < 0 || ledger.ReservedAt < 0 || ledger.Quarantined && (ledger.Pending || ledger.Committed) ||
                    ledger.Delta < -int.MaxValue || ledger.Delta > int.MaxValue ||
                    (ledger.Pending && ((long)balance + Math.Abs(ledger.Delta) > int.MaxValue)) ||
                    ledger.Actor.Length > 128 || ledger.Token.Length > 128 || ledger.Epoch.Length > 128 ||
                    ledger.Fingerprint.Length > 128 || stream.Position != stream.Length ||
                    (ledger.Pending && (ledger.Actor.Length == 0 || ledger.Token.Length == 0 || ledger.Epoch.Length == 0 || ledger.Fingerprint.Length == 0)))
                    throw new InvalidDataException("Invalid ledger state.");
                return ledger;
            }
        }
    }
}
