// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Almanac.Runtime.UI
{
    internal static class ConfigOwnerPresentation
    {
        internal static string Phrase(NativeWriteFacts facts)
        {
            if (facts == null || !facts.Known) return "Owner: Unknown";
            switch (facts.DeclaredScope)
            {
                case ConfigScope.Local: return "Owner: Each player";
                case ConfigScope.ServerAuthoritative:
                case ConfigScope.Cheat: return "Owner: Host or server";
                default: return "Owner: Unknown";
            }
        }
    }
}
