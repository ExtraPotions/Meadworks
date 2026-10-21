// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
namespace ExtraPotions.Cairn.Runtime
{
    internal static class OptionalSailingApi
    {
        internal static Type? FindType(IEnumerable<Assembly> assemblies, string name)
        {
            foreach (var assembly in assemblies)
            {
                var type = assembly.GetType(name, throwOnError: false, ignoreCase: false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
