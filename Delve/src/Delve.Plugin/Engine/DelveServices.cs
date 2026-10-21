// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the services the regeneration engine runs, in sweep order.
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Delve.Runtime
{
    public static class DelveServices
    {
        public static DelveEngine Create(MeadworksLog log)
        {
            var engine = new DelveEngine(log);
            engine.Add(new DelveChests());
            engine.Add(new DelveDungeons());
            engine.Add(new DelveKallRoom());
            engine.Add(new DelveWorldObjects());
            engine.Add(new DelveSpawners());
            engine.Add(new DelveFullReset());
            return engine;
        }
    }
}
