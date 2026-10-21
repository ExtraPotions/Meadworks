// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries.
namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>How the fractional return of one material becomes a whole number (setting "Rounding").</summary>
    public enum RoundingMode
    {
        Floor,
        Round,
        Ceil,
    }
}
