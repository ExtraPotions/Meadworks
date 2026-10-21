// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Silver Tongue (<c>trader_discount</c>): traders (Haldor, Hildir, the Bog Witch: every <c>Trader</c> the game's
    /// store window opens) charge X% less. The store reads each item's <c>m_price</c> in three places: the list it
    /// draws (<c>FillList</c>), whether the player can afford it (<c>CanAfford</c>, also the buy button every frame) and
    /// the coins it takes (<c>BuySelectedItem</c>). Each of them runs inside a price window: the open trader's prices
    /// are lowered on entry and put back on exit (a finalizer, so even a failure restores them), so the shown price,
    /// the check and the coins taken are always the same number and nothing outside the window ever sees a changed
    /// price. Windows nest (buying redraws the list). A price is rounded up and never drops below 1 (a free item stays
    /// free). Trading is the local player's own client: the coins and the bought item are in its inventory.
    /// </summary>
    internal static class TraderDiscount
    {
        private static readonly List<KeyValuePair<Trader.TradeItem, int>> Saved = new List<KeyValuePair<Trader.TradeItem, int>>();
        private static int _depth;

        public static void Open(StoreGui store)
        {
            _depth++;
            if (_depth > 1)
            {
                return;
            }
            float discount = Mathf.Min(0.95f, AggregateHost.Current[EffectKind.TraderDiscount]);
            Trader? trader = store != null ? store.m_trader : null;
            if (discount > 0f && trader != null && Player.m_localPlayer != null)
            {
                Lower(trader, discount);
            }
        }

        public static void Close()
        {
            if (_depth > 0 && --_depth > 0)
            {
                return;
            }
            for (int i = Saved.Count - 1; i >= 0; i--)
            {
                Saved[i].Key.m_price = Saved[i].Value;
            }
            Saved.Clear();
        }

        private static void Lower(Trader trader, float discount)
        {
            foreach (Trader.TradeItem item in trader.m_items)
            {
                if (item != null && item.m_price > 0)
                {
                    Saved.Add(new KeyValuePair<Trader.TradeItem, int>(item, item.m_price));
                    item.m_price = Mathf.Max(1, Mathf.CeilToInt(item.m_price * (1f - discount) - 0.001f));
                }
            }
        }
    }

    /// <summary>The store's three price readers, each inside a price window (see <see cref="TraderDiscount"/>).</summary>
    [HarmonyPatch]
    internal static class TraderDiscountPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(StoreGui), nameof(StoreGui.FillList));
            yield return AccessTools.Method(typeof(StoreGui), nameof(StoreGui.CanAfford));
            yield return AccessTools.Method(typeof(StoreGui), nameof(StoreGui.BuySelectedItem));
        }

        private static void Prefix(StoreGui __instance) => TraderDiscount.Open(__instance);

        private static void Finalizer() => TraderDiscount.Close();
    }
}
