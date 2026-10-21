// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Native bindings for the requester's evidence, status probe and settlement release. Not linked into the portable owner
// runner, which supplies its own delegates.
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Runtime;

internal static partial class TheftTransfers
{
    static partial void Bind()
    {
        Proof ??= ThievingArchive.Proof;
        Probe ??= ThievingStatus.Ask;
        Settled ??= ThievingStatus.Acknowledge;
        ReleaseRow ??= ThievingArchive.Release;
        Clock ??= () => ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : double.NaN;
    }
}
