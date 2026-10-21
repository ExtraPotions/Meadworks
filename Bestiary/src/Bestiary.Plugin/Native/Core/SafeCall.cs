// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters;
// the three-argument Func form from EliteCreaturesPack 0.9.0 (ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;

namespace ExtraPotions.Bestiary.Runtime.Native.Core
{
    /// <summary>
    /// For patches that run inside a game call the mod must never break - a chest filling, a creature spawning, a hit
    /// landing. A failure is reported under the mod's name and swallowed, so the game's own step still happens and the
    /// mod's part is simply skipped. On hot paths (a patch on every creature's AI, every ship's physics step, every hit)
    /// use the overloads that take the arguments, with a static lambda: a lambda that captures a parameter allocates its
    /// closure on every call, at the start of the patch method, before any early return in it.
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

        /// <summary>Same as <see cref="Run(string, Action)"/> with the argument passed in: a static lambda allocates nothing.</summary>
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

        /// <summary>Same as <see cref="Run{TArg}(string, Action{TArg}, TArg)"/> with two arguments.</summary>
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

        /// <summary>Same as <see cref="Run{TArg}(string, Action{TArg}, TArg)"/> with three arguments.</summary>
        public static void Run<T1, T2, T3>(string context, Action<T1, T2, T3> action, T1 first, T2 second, T3 third)
        {
            try
            {
                action(first, second, third);
            }
            catch (Exception e)
            {
                Guard.Report(e, context);
            }
        }

        /// <summary>The answer of `func`, or `fallback` when it fails (reported and swallowed).</summary>
        public static T Run<TArg, T>(string context, Func<TArg, T> func, TArg arg, T fallback)
        {
            try
            {
                return func(arg);
            }
            catch (Exception e)
            {
                Guard.Report(e, context);
                return fallback;
            }
        }

        /// <summary>Same as <see cref="Run{TArg, T}(string, Func{TArg, T}, TArg, T)"/> with two arguments.</summary>
        public static T Run<T1, T2, T>(string context, Func<T1, T2, T> func, T1 first, T2 second, T fallback)
        {
            try
            {
                return func(first, second);
            }
            catch (Exception e)
            {
                Guard.Report(e, context);
                return fallback;
            }
        }

        /// <summary>Same as the two-argument form with three arguments (EliteCreaturesPack 0.9.0).</summary>
        public static T Run<T1, T2, T3, T>(string context, Func<T1, T2, T3, T> func, T1 first, T2 second, T3 third, T fallback)
        {
            try
            {
                return func(first, second, third);
            }
            catch (Exception e)
            {
                Guard.Report(e, context);
                return fallback;
            }
        }
    }
}
