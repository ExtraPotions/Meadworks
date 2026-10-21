// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Inscribe.Config;
using Authority = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Inscribe.Runtime
{
    internal static class InscribeLootInterop
    {
        internal static int Failures { get; private set; }
        internal static Tuple<int, bool, bool, bool, int>? DeathFacts(Character creature)
        {
            try
            {
                return Authority.Services.Capabilities.TryResolve<Func<object, Tuple<int, bool, bool, bool, int>>>("meadworks.loot.drops", new System.Version(1, 0), out var read) ? read!(creature) : null;
            }
            catch (Exception error) { Failed("Wildblood death facts", error); return null; }
        }
        internal static bool StarSynergy => Plugin.Settings?.Get(InscribeSettings.Drops.WildbloodStarSynergy) == true;
        internal static int StackAmount(ItemDrop.ItemData item, int amount)
        {
            if (amount <= 0 || item.m_shared.m_maxStackSize <= 1) return amount;
            try
            {
                return Authority.Services.Capabilities.TryResolve<Func<object, int, int>>("meadworks.loot.multiplier", new System.Version(1, 0), out var multiply) ? Math.Max(0, Math.Min(100000, multiply!(item, amount))) : amount;
            }
            catch (Exception error) { Failed("Magpie loot amount", error); return amount; }
        }
        private static void Failed(string label, Exception error) { Failures++; if (Failures == 1) Adopted.Core.Log.Error(label + ": " + error.Message); }
    }
}
