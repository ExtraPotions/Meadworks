// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Authority = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Magpie.Runtime.Loot
{
    internal static class WildbloodMultiplier
    {
        internal const string Capability = "meadworks.loot.creature_multiplier";
        static readonly System.Version Version = new System.Version(1, 0);
        internal static float Read()
        {
            try
            {
                if (!Authority.IsReady || !Authority.Services.Capabilities.TryResolve<Func<float>>(Capability, Version, out var read)) return 1f;
                float value = read!();
                return float.IsNaN(value) || float.IsInfinity(value) ? 1f : value;
            }
            catch { return 1f; }
        }
    }
}
