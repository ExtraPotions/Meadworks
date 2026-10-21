// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Inscribe.Interop
{
    /// <summary>Opaque native item input; strings and tuples keep consumers independent of Inscribe's assembly.</summary>
    public static class DecorationV1
    {
        public const string Capability = "meadworks.items.decoration";
        public static readonly Version Version = new Version(1, 0);
        // Row: stable key, localized label, summed stored value, flat|percent|flag, Higher|Lower|None, localized tooltip text.
        public static Type ContractType => typeof(Tuple<Func<object, string>, Func<object, string>, Func<object, Tuple<string, string, float, string, string, string>[]>>);
    }
}
