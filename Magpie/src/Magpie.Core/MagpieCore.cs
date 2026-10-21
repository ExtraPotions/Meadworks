// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Magpie
{
    /// <summary>Magpie.Core identity: Unity-free logic shared by the plugin and the portable tests.</summary>
    public static class MagpieCore
    {
        public const string ProductId = "com.meadworks.magpie";

        /// <summary>ZDO key that marks a ground item as world loot for the loot multiplier (design 3.4).</summary>
        public const string LootMarkKey = "magpie.loot";

        /// <summary>
        /// ZDO key Magpie writes (int 1) on a ground item whose <c>ItemData.m_cheated</c> is set, because the game does not
        /// save that flag in a drop's ZDO (V6); load restores the flag from it (user decision 2026-10-07).
        /// </summary>
        public const string CheatedMarkKey = "magpie.cheated";

        /// <summary>ZDO key of the persisted instance token used by the merge protocol (design 3.3, Crucible C12).</summary>
        public const string InstanceTokenKey = "magpie.id";

        /// <summary>ZDO key of the phase 1 merge intent record on the target (design 3.3).</summary>
        public const string MergeInKey = "magpie.merge_in";

        /// <summary>ZDO key of the phase 2 nonce on a partially drained source (design 3.3).</summary>
        public const string MergeOutKey = "magpie.merge_out";
    }
}
