// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Magpie.Merging
{
    public enum BarrierState
    {
        /// <summary>Phase 2 may follow phase 1 at once.</summary>
        Pass,

        /// <summary>No barrier can be confirmed here; the merge must not start.</summary>
        Unavailable,
    }

    /// <summary>Where the merging peer runs.</summary>
    public enum MergeRole
    {
        None,
        SinglePlayer,

        /// <summary>Dedicated server or host: the ZDO authority, so both phases are recorded locally in order.</summary>
        Server,

        RemoteClient,
    }

    /// <summary>
    /// Send barrier of design section 3.3 step 4. Single player has no peers. On the server or host the ZDO changes are
    /// made in the authority's own store and sent to each peer over one reliable ordered channel, so phase 2 can never
    /// arrive before phase 1. A remote client would need the per-peer sent revision (verify V5); until the G01 probe
    /// records it, a remote client does not merge at all. This is the no-loss interim (same effect as Q5 option a);
    /// the user still decides Q5 if V5 fails.
    /// </summary>
    public static class MergeBarrier
    {
        public static BarrierState Decide(MergeRole role) =>
            role == MergeRole.SinglePlayer || role == MergeRole.Server ? BarrierState.Pass : BarrierState.Unavailable;

        public static bool MayMerge(MergeRole role) => Decide(role) == BarrierState.Pass;
    }
}
