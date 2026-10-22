// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Collections.Generic;

namespace ExtraPotions.Barter.Runtime
{
    internal sealed class AuthorityReceipt
    {
        internal string Actor = "", Fingerprint = "", Epoch = "";
        // 0 reserved, 1/2 verified success/rollback, 3 legacy report, 4 expired, 5/6 reported success/rollback.
        internal byte State;
        internal bool Applied, CharacterConsumed;
        private static readonly Dictionary<ZDOID, string> rotated = new Dictionary<ZDOID, string>();
        private static string rotationEpoch = "";
        internal static void Rotate(ZDO zdo, ExtraPotions.Barter.Trading.PurseLedger ledger, string epoch)
        {
            if (rotationEpoch != epoch) { rotated.Clear(); rotationEpoch = epoch; }
            if (rotated.TryGetValue(zdo.m_uid, out string previousEpoch) && previousEpoch == epoch) return;
            foreach (string field in AuthorityState.Fields(zdo, "receipt-"))
            {
                string token = field.Substring("receipt-".Length);
                if (token == ledger.Token) continue;
                var receipt = Read(zdo, token);
                if (receipt != null && receipt.Applied && receipt.Epoch != epoch)
                    AuthorityState.Remove(zdo, field);
            }
            rotated[zdo.m_uid] = epoch;
        }
        internal static void FinalizePrevious(ZDO zdo, ExtraPotions.Barter.Trading.PurseLedger ledger)
        {
            if (ledger.Pending || ledger.Token.Length == 0) return;
            var previous = Read(zdo, ledger.Token);
            if (previous == null || previous.Applied) return;
            if (previous.Actor != ledger.Actor || previous.Fingerprint != ledger.Fingerprint)
                throw new InvalidDataException("Prior settlement receipt mismatch");
            previous.State = ledger.Quarantined ? (byte)4 : previous.State == 5 || previous.State == 6
                ? previous.State : ledger.Committed ? (byte)1 : (byte)2;
            previous.Applied = true;
            previous.Save(zdo, ledger.Token);
        }
        internal static AuthorityReceipt Read(ZDO zdo, string token)
        {
            string encoded = AuthorityState.Read(zdo, "receipt-" + token);
            if (encoded.Length == 0) return null;
            using (var stream = new MemoryStream(Convert.FromBase64String(encoded)))
            using (var reader = new BinaryReader(stream))
            {
                byte version = reader.ReadByte();
                if (version != 1 && version != 2) throw new InvalidDataException("Receipt version");
                var receipt = new AuthorityReceipt { Actor = reader.ReadString(), Fingerprint = reader.ReadString(), State = reader.ReadByte(), Applied = reader.ReadBoolean() };
                if (version >= 2) { receipt.Epoch = reader.ReadString(); receipt.CharacterConsumed = reader.ReadBoolean(); }
                else receipt.CharacterConsumed = receipt.State == 1 || receipt.State == 5;
                if (receipt.Actor.Length == 0 || receipt.Actor.Length > 128 || receipt.Fingerprint.Length != 44 || receipt.Epoch.Length > 128 || receipt.State > 6 || stream.Position != stream.Length)
                    throw new InvalidDataException("Receipt state");
                return receipt;
            }
        }
        internal void Save(ZDO zdo, string token)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)2); writer.Write(Actor); writer.Write(Fingerprint); writer.Write(State); writer.Write(Applied);
                writer.Write(Epoch); writer.Write(CharacterConsumed);
                AuthorityState.Write(zdo, "receipt-" + token, Convert.ToBase64String(stream.ToArray()));
            }
        }
    }
}
