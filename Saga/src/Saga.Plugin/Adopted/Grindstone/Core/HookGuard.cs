// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/HookGuard.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Runs one feature's hook from inside the game's own code (a tree falling, a log or a rock chunk breaking, a
    /// swing), where an exception would abort the rest of the game's method: a tree that never drops, a chunk that
    /// never spawns its ore. A failing hook is logged under GrindstoneSkills and swallowed, so the other features and
    /// the game carry on. Shared by the Woodcutting and Pickaxes modules; it runs on whichever machine calls it.
    /// </summary>
    public static class HookGuard
    {
        public static void Run(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                NativeSaga.Report(exception, context);
            }
        }

        public static T Run<T>(string context, Func<T> func, T fallback)
        {
            try
            {
                return func();
            }
            catch (Exception exception)
            {
                NativeSaga.Report(exception, context);
                return fallback;
            }
        }

        /// <summary>
        /// Run with the hook's argument passed in, for hooks that run several times a second (every plant's slow update):
        /// a static lambda captures nothing, so it allocates nothing, where a capturing one allocates on every call.
        /// </summary>
        public static void Run<TArg>(string context, Action<TArg> action, TArg arg)
        {
            try
            {
                action(arg);
            }
            catch (Exception exception)
            {
                NativeSaga.Report(exception, context);
            }
        }

        /// <summary>Same as <see cref="Run{TArg}(string, Action{TArg}, TArg)"/> for a hook that returns a value.</summary>
        public static T Run<TArg, T>(string context, Func<TArg, T> func, TArg arg, T fallback)
        {
            try
            {
                return func(arg);
            }
            catch (Exception exception)
            {
                NativeSaga.Report(exception, context);
                return fallback;
            }
        }
    }
}
