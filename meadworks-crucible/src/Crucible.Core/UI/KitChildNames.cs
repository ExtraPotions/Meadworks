// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Kit parts found again by name (a button, a glyph) must never pick up a child that is already on its way out. Unity
    /// destroys an object only at the end of the frame, so until then <c>Transform.Find</c> still returns it. The kit's
    /// safe-destroy helper (Crucible.UI <c>KitUnity.Retire</c>) renames a child with <see cref="RetiredPrefix"/> before it
    /// destroys it, and every kit lookup by name skips retired names.
    /// </summary>
    public static class KitChildNames {
        public const string RetiredPrefix="Crucible_retired_";

        /// <summary>The name a child carries from retirement until Unity destroys it.</summary>
        public static string Retired(string? name)=>IsRetired(name)?name!:RetiredPrefix+(name??"");

        public static bool IsRetired(string? name)=>name!=null&&name.StartsWith(RetiredPrefix,System.StringComparison.Ordinal);

        /// <summary>A child named <paramref name="child"/> may be reused for <paramref name="wanted"/>: same name, and not retired.</summary>
        public static bool Reusable(string? child,string? wanted)=>!string.IsNullOrEmpty(wanted)&&string.Equals(child,wanted,System.StringComparison.Ordinal)&&!IsRetired(child);
    }
}
