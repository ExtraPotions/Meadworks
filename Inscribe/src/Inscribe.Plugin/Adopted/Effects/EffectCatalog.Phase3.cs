// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The Phase 3 effects (features/effects-phase3.md): the inscriptions added with item classes and tier ladders.
    /// Each area registers its own ids in its own partial file; an area not built yet registers nothing.
    /// </summary>
    internal static partial class EffectCatalog
    {
        private static void RegisterPhase3()
        {
            RegisterPhase3Combat();
            RegisterPhase3Survival();
            RegisterPhase3Gathering();
            RegisterPhase3Throwing();
        }

        static partial void RegisterPhase3Combat();

        static partial void RegisterPhase3Survival();

        static partial void RegisterPhase3Gathering();

        static partial void RegisterPhase3Throwing();
    }
}
