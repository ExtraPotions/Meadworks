// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ExtraPotions.Shepherd.Breeding;
using TraitsProvider = System.Tuple<System.Func<object, string>, System.Func<string, string, int, float, int, string>,
    System.Action<object, string>, System.Func<string, string>, System.Func<string, string>>;

namespace ExtraPotions.Shepherd.Runtime.Integration
{
    /// <summary>Resolve the optional provider at each native operation; never retain a removed product's delegates.</summary>
    internal static class WildbloodTraitsCapability
    {
        internal static TraitsConsumer Current
        {
            get
            {
                try
                {
                    return new TraitsConsumer(CruciblePlugin.Services.Capabilities.TryResolve<TraitsProvider>(
                        TraitsConsumer.Id, TraitsConsumer.Version, out var provider) ? provider : null);
                }
                catch (Exception) { return new TraitsConsumer(null); }
            }
        }
    }
}
