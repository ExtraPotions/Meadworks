// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtraPotions.Barter.Config;
using ExtraPotions.Barter.Trading;

namespace ExtraPotions.Barter.Runtime
{
    internal sealed class BuybackOffer
    {
        internal string Id = "", Actor = "", Label = "", Currency = "", Snapshot = "", ReservedBy = "";
        internal int Amount, Price, Expires;
    }

    // Only the authority writes this trader-bound escrow. Expiry is based on world days, never a client's clock.
    internal static class TradeFeatures
    {
        private static readonly int Key = "meadworks.barter.escrow.v1".GetStableHashCode();
        internal static int Day => EnvMan.instance != null ? EnvMan.instance.GetCurrentDay() : 0;

        internal static List<BuybackOffer> Read(ZDO zdo)
        {
            string encoded = zdo == null ? "" : AuthorityState.Read(zdo, "escrow", zdo.GetString(Key, ""));
            var rows = new List<BuybackOffer>();
            if (encoded.Length == 0) return rows;
            if (encoded.Length > 1048576) throw new InvalidDataException("Escrow storage bound");
            using (var stream = new MemoryStream(Convert.FromBase64String(encoded)))
            using (var reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                if (version != 1 && version != 2) throw new InvalidDataException("Escrow version");
                int count = reader.ReadInt32(); if (count < 0 || count > 64) throw new InvalidDataException("Escrow count");
                for (int i = 0; i < count; i++)
                {
                    var row = new BuybackOffer { Id = reader.ReadString(), Actor = reader.ReadString(), Label = reader.ReadString(),
                        Currency = reader.ReadString(), Snapshot = reader.ReadString(), Amount = reader.ReadInt32(), Price = reader.ReadInt32(), Expires = reader.ReadInt32() };
                    if (version == 2) row.ReservedBy = reader.ReadString();
                    if (row.ReservedBy.Length > 128 || row.Id.Length > 128 || row.Actor.Length > 128 || row.Label.Length > 256 || row.Currency.Length > 128 || row.Snapshot.Length > 65536 || row.Amount <= 0 || row.Price <= 0)
                        throw new InvalidDataException("Escrow row");
                    rows.Add(row);
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Escrow trailing data");
            }
            return rows;
        }

        private static void Write(ZDO zdo, List<BuybackOffer> rows)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) throw new InvalidOperationException("Authority only");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(2); writer.Write(rows.Count);
                foreach (var row in rows)
                {
                    writer.Write(row.Id); writer.Write(row.Actor); writer.Write(row.Label); writer.Write(row.Currency); writer.Write(row.Snapshot);
                    writer.Write(row.Amount); writer.Write(row.Price); writer.Write(row.Expires);
                    writer.Write(row.ReservedBy);
                }
                string encoded = Convert.ToBase64String(stream.ToArray());
                if (encoded.Length > 1048576) throw new InvalidDataException("Escrow full");
                AuthorityState.Write(zdo, "escrow", encoded);
                zdo.SetOwner(ZDOMan.GetSessionID());
                zdo.Set(Key, encoded);
            }
        }

        internal static bool Validate(string actor, TradeRequest request, Trader trader, ZDO zdo, out long delta, bool inventoryVerified = false)
        {
            delta = 0;
            if (!inventoryVerified) return false;
            string name = TradeWorld.TraderName(trader);
            if (request.Service == 1)
            {
                if (!request.Buy || !Plugin.Settings.GetFor(BarterSettings.Services.BuybackEnabled, name)) return false;
                var offer = Read(zdo).FirstOrDefault(r => r.Id == request.Receipt && r.Actor == actor && r.Expires > Day && r.ReservedBy.Length == 0);
                if (offer == null || request.Snapshot != offer.Snapshot || request.Amount != offer.Amount || request.Price != offer.Price ||
                    request.Currency != offer.Currency || !TradeItemSnapshot.Matches(request)) return false;
                if (TradeItemSnapshot.Decode(request.Snapshot).Any(i => i.Item.m_equipped)) return false;
            }
            else if (request.Service == 2)
            {
                if (!request.Buy || !Plugin.Settings.GetFor(BarterSettings.Services.RepairEnabled, name) || request.Amount != 1 ||
                    request.LotSize != 1 || !TradeItemSnapshot.Matches(request)) return false;
                var item = TradeItemSnapshot.Decode(request.Snapshot).Single().Item;
                var currency = Currencies.OfTrader(name);
                if (!item.m_shared.m_useDurability || !item.m_shared.m_canBeReparied || item.m_stack != 1 || currency == null ||
                    request.Currency != Utils.GetPrefabName(currency.gameObject) ||
                    !ServiceRules.RepairPrice(Plugin.Settings.GetFor(BarterSettings.Services.RepairBasePrice, name), item.m_quality,
                        item.m_durability, item.GetMaxDurability(), Purses.BuyFactor(trader, zdo), out int price) || price != request.Price) return false;
            }
            else return false;
            var own = Currencies.OfTrader(name);
            if (own != null && Purses.Applies(trader) && request.Currency == Utils.GetPrefabName(own.gameObject)) delta = request.Price;
            return true;
        }

        internal static bool CanCapture(TradeRequest request, Trader trader, ZDO zdo)
        {
            if (request.Service != 0 || request.Buy || request.Snapshot.Length == 0) return true;
            if (!TradeItemSnapshot.Matches(request)) return false;
            if (TradeItemSnapshot.Decode(request.Snapshot).Any(i => i.Item.m_equipped)) return false;
            if (!Plugin.Settings.GetFor(BarterSettings.Services.BuybackEnabled, TradeWorld.TraderName(trader))) return false;
            var rows = Read(zdo).Where(r => r.Expires > Day || r.ReservedBy.Length > 0).ToList();
            return rows.Count < 64 && rows.Sum(r => (long)r.Snapshot.Length) + request.Snapshot.Length < 700000;
        }

        internal static void Finish(string actor, TradeRequest request, bool success, ZDO zdo, bool inventoryVerified = false)
        {
            if (!inventoryVerified) return;
            var rows = Read(zdo);
            if (request.Service == 1)
            {
                if (success) rows.RemoveAll(r => r.Id == request.Receipt && r.Actor == actor && r.ReservedBy == request.Token);
                else foreach (var row in rows)
                    if (row.Id == request.Receipt && row.Actor == actor && row.ReservedBy == request.Token) row.ReservedBy = "";
            }
            else if (!success) return;
            else if (request.Service == 0 && !request.Buy && request.Snapshot.Length > 0 && !rows.Any(r => r.Id == request.Token))
            {
                // Capture was validated at reservation. Live disabling does not discard an already paid sale receipt.
                rows.RemoveAll(r => r.Expires <= Day && r.ReservedBy.Length == 0);
                rows.Add(new BuybackOffer { Id = request.Token, Actor = actor, Label = request.Prefab, Currency = request.Currency,
                    Snapshot = request.Snapshot, Amount = request.Amount, Price = request.Price, Expires = Day + 1 });
            }
            else return;
            Write(zdo, rows);
        }

        internal static void ReserveBuyback(string actor, TradeRequest request, ZDO zdo)
        {
            var rows = Read(zdo);
            var row = rows.Single(r => r.Id == request.Receipt && r.Actor == actor && r.ReservedBy.Length == 0);
            row.ReservedBy = request.Token;
            Write(zdo, rows);
        }
    }
}
