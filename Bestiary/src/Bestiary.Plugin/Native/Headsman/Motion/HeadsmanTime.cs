// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The world's clock, the same on every peer (the server's), for the moments kept in ZDOs: when an axe broke, when a
    /// skeleton began to form. Kept as whole milliseconds (a float loses the fraction of a second in a long world).
    /// </summary>
    public static class HeadsmanTime
    {
        public static double Now => ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0d;

        public static void Keep(ZDO zdo, string key, double seconds) => zdo.Set(key, (long)(seconds * 1000d));

        /// <summary>The moment kept under `key`, or 0 when none is.</summary>
        public static double Read(ZDO zdo, string key) => zdo.GetLong(key) / 1000d;
    }
}
