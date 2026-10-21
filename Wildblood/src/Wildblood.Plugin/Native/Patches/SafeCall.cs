// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// For patches that run inside a game call the mod must never break - an altar spending its offering, the server
    /// storing a global key, a creature giving birth. A failure is reported under the mod's name and swallowed, so the
    /// game's own step still happens and the mod's part is simply skipped.
    /// </summary>
    internal static class SafeCall
    {
        public static void Run(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Guard.Report(e, context);
            }
        }

        /// <summary>The same with its argument passed in, for a hot path: a static lambda captures and allocates nothing.</summary>
        public static void Run<TArg>(string context, Action<TArg> action, TArg arg)
        {
            try
            {
                action(arg);
            }
            catch (Exception e)
            {
                Guard.Report(e, context);
            }
        }

        /// <summary>The same with two arguments.</summary>
        public static void Run<T1, T2>(string context, Action<T1, T2> action, T1 first, T2 second)
        {
            try
            {
                action(first, second);
            }
            catch (Exception e)
            {
                Guard.Report(e, context);
            }
        }
    }
}
