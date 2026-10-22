// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Security.Cryptography;
using ExtraPotions.Barter.Trading;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Barter.Runtime
{
    internal sealed class TradeRequest
    {
        internal bool Buy, Vanilla;
        internal string Prefab = "", Currency = "", Token = "", Epoch = "";
        internal int Amount, Quality, LotSize, Price;
        internal byte Service;
        internal string Receipt = "", Snapshot = "";
        internal long TraderUser;
        internal uint TraderId;
    }

    internal sealed class FinishRequest { internal TradeRequest Trade = new TradeRequest(); internal bool Success; }

    internal sealed class TradeCodec : IOperationCodec<TradeRequest, bool>
    {
        internal const int MaxOperationBytes = 16384;
        // The finish request prefixes the same trade body with its outcome byte.
        internal const int MaxTradeBytes = MaxOperationBytes - 1;

        internal static bool FitsReservation(TradeRequest value, string epoch)
        {
            try { new TradeCodec().EncodeRequest(value, new string('0', 32), epoch); return true; }
            catch (InvalidDataException) { return false; }
        }
        internal static string Fingerprint(TradeRequest value)
        {
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(new TradeCodec().EncodeRequest(value)));
        }
        internal static bool MatchesReceipt(PurseLedger ledger, TradeRequest value) => ledger.Fingerprint == Fingerprint(value);
        public byte[] EncodeRequest(TradeRequest value) => EncodeRequest(value, value.Token, value.Epoch);
        private byte[] EncodeRequest(TradeRequest value, string token, string epoch)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(value.Buy); writer.Write(value.Vanilla); writer.Write(value.TraderUser); writer.Write(value.TraderId);
                writer.Write(value.Prefab); writer.Write(value.Currency); writer.Write(value.Amount); writer.Write(value.Quality);
                writer.Write(value.LotSize); writer.Write(value.Price); writer.Write(token); writer.Write(epoch);
                writer.Write(value.Service); writer.Write(value.Receipt); writer.Write(value.Snapshot);
                if (stream.Length > MaxTradeBytes) throw new InvalidDataException("Trade payload exceeds operation bound");
                return stream.ToArray();
            }
        }
        public TradeRequest DecodeRequest(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 2 || bytes.Length > MaxTradeBytes || bytes[0] > 1 || bytes[1] > 1) throw new InvalidDataException();
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                var value = new TradeRequest { Buy = reader.ReadBoolean(), Vanilla = reader.ReadBoolean(), TraderUser = reader.ReadInt64(),
                    TraderId = reader.ReadUInt32(), Prefab = reader.ReadString(), Currency = reader.ReadString(), Amount = reader.ReadInt32(),
                    Quality = reader.ReadInt32(), LotSize = reader.ReadInt32(), Price = reader.ReadInt32(), Token = reader.ReadString(), Epoch = reader.ReadString() };
                value.Service = reader.ReadByte(); value.Receipt = reader.ReadString(); value.Snapshot = reader.ReadString();
                if (stream.Position != stream.Length || value.Prefab.Length > 128 || value.Currency.Length > 128 || value.Token.Length > 128 || value.Epoch.Length > 128 || value.Service > 2 || value.Receipt.Length > 128 || value.Snapshot.Length > 32768)
                    throw new InvalidDataException();
                return value;
            }
        }
        public byte[] EncodeReply(bool reply) => new[] { reply ? (byte)1 : (byte)0 };
        public bool DecodeReply(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 1 || bytes[0] > 1) throw new InvalidDataException();
            return bytes[0] == 1;
        }
    }

    internal sealed class FinishCodec : IOperationCodec<FinishRequest, bool>
    {
        public byte[] EncodeRequest(FinishRequest value)
        {
            byte[] trade = new TradeCodec().EncodeRequest(value.Trade);
            var bytes = new byte[trade.Length + 1]; bytes[0] = value.Success ? (byte)1 : (byte)0;
            Buffer.BlockCopy(trade, 0, bytes, 1, trade.Length); return bytes;
        }
        public FinishRequest DecodeRequest(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 2 || bytes.Length > TradeCodec.MaxOperationBytes || bytes[0] > 1) throw new InvalidDataException();
            var trade = new byte[bytes.Length - 1]; Buffer.BlockCopy(bytes, 1, trade, 0, trade.Length);
            return new FinishRequest { Trade = new TradeCodec().DecodeRequest(trade), Success = bytes[0] == 1 };
        }
        public byte[] EncodeReply(bool reply) => new TradeCodec().EncodeReply(reply);
        public bool DecodeReply(byte[] bytes) => new TradeCodec().DecodeReply(bytes);
    }
}
