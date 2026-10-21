// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: reads the world tier from the global keys the server keeps (design A3, section 8 World tier row).
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Stars;

namespace ExtraPotions.Wildblood.Runtime
{
    /// <summary>
    /// The world tier now: one per listed boss key set in the world, unless overridden. World settings bind as
    /// ServerAuthoritative until Crucible CS4 stores them with the world. Read only; the announcement is B03b.
    /// </summary>
    public static class WorldTierReader
    {
        public static int Current()
        {
            var s = Plugin.Settings;
            if (s == null || ZoneSystem.instance == null) return 0;
            return WorldTier.Compute(s.Get(WildbloodSettings.WorldTier.Enabled), s.Get(WildbloodSettings.WorldTier.BossKeys),
                s.Get(WildbloodSettings.WorldTier.Override), key => ZoneSystem.instance.GetGlobalKey(key));
        }
    }
}
