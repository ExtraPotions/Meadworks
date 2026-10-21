// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// Every endpoint of <see cref="InscribeApi"/> runs through here: an exception inside ExtraPotions.Inscribe.Adopted is logged
    /// once per endpoint and the caller gets the endpoint's empty answer (false, null, 0), never the exception. The
    /// overloads take the arguments separately so a static method group needs no closure (hot endpoints are read every
    /// frame by some callers).
    /// </summary>
    internal static class ApiGuard
    {
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        public static T Run<T>(string endpoint, Func<T> call, T fallback)
        {
            try
            {
                return call();
            }
            catch (Exception e)
            {
                return Failed(endpoint, e, fallback);
            }
        }

        public static T Run<A, T>(string endpoint, Func<A, T> call, A a, T fallback)
        {
            try
            {
                return call(a);
            }
            catch (Exception e)
            {
                return Failed(endpoint, e, fallback);
            }
        }

        public static T Run<A, B, T>(string endpoint, Func<A, B, T> call, A a, B b, T fallback)
        {
            try
            {
                return call(a, b);
            }
            catch (Exception e)
            {
                return Failed(endpoint, e, fallback);
            }
        }

        public static T Run<A, B, C, T>(string endpoint, Func<A, B, C, T> call, A a, B b, C c, T fallback)
        {
            try
            {
                return call(a, b, c);
            }
            catch (Exception e)
            {
                return Failed(endpoint, e, fallback);
            }
        }

        private static T Failed<T>(string endpoint, Exception e, T fallback)
        {
            if (Reported.Add(endpoint))
            {
                Log.Error($"API {endpoint} failed; it answers with nothing (logged once): {e}");
            }
            return fallback;
        }
    }

    /// <summary>The endpoint names of <see cref="InscribeApi"/>: its public static methods, read once by reflection.</summary>
    internal static class ApiEndpoints
    {
        private static HashSet<string>? _names;

        public static bool Has(string? name) => name != null && Names().Contains(name);

        /// <summary>Every endpoint name, sorted.</summary>
        public static string[] All()
        {
            List<string> names = new List<string>(Names());
            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        private static HashSet<string> Names()
        {
            if (_names == null)
            {
                HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
                foreach (MethodInfo method in typeof(InscribeApi).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    names.Add(method.Name);
                }
                _names = names;
            }
            return _names;
        }
    }
}
