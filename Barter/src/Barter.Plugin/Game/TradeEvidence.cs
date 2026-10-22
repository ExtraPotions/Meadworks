// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Barter.Runtime
{
    internal static class TradeEvidence
    {
        internal static bool MatchesInventory(TradeRequest request, bool sale)
        {
            var player = Player.m_localPlayer;
            if (player == null) return false;
            try
            {
                var used = new HashSet<ItemDrop.ItemData>();
                var actual = new List<SafeTradeInventory.Take>();
                var expectedItems = TradeItemSnapshot.Decode(request.Snapshot);
                foreach (var expected in expectedItems)
                {
                    string encoded = TradeItemSnapshot.Encode(new[] { expected });
                    var item = player.GetInventory().GetAllItems().FirstOrDefault(candidate => !used.Contains(candidate) &&
                        candidate.m_stack >= expected.Amount && (!sale || !candidate.m_equipped && !OptionalTradeIntegrations.ProtectedItem(candidate, player)) &&
                        TradeItemSnapshot.Encode(new[] { new SafeTradeInventory.Take(candidate, expected.Amount) }) == encoded);
                    if (item == null) return false;
                    used.Add(item); actual.Add(new SafeTradeInventory.Take(item, expected.Amount));
                }
                return TradeItemSnapshot.Encode(actual) == TradeItemSnapshot.Encode(expectedItems);
            }
            catch (Exception) { return false; }
        }
    }
}
