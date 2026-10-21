// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Bestiary
{
    /// <summary>Bestiary.Core identity: Unity-free logic shared by the plugin and the portable tests.</summary>
    public static class BestiaryCore
    {
        public const string ProductId = "com.meadworks.bestiary";
        public const string ProductName = "Bestiary";
        public const string Family = "Fang";
        public const string Version = "0.1.15";

        /// <summary>Game build the design was written against (design header).</summary>
        public const string GameBuild = "1.0.17";
    }

    /// <summary>
    /// ZDO keys, RPC names and global key prefixes. User decision Q2 (2026-10-07): EliteCreaturesPack's names are kept
    /// unchanged so worlds, inventories and other mods' configs carry over with no conversion.
    /// </summary>
    public static class BestiaryKeys
    {
        /// <summary>True on a dormant mimic (Wildblood reads it through <c>meadworks.creatures.disguise</c> v1).</summary>
        public const string Disguised = "ecp_disguised";

        /// <summary>Chest marked as a mimic when its crypt first filled.</summary>
        public const string MimicChest = "ecp_mimic_chest";

        /// <summary>Swap pending on the chest owner.</summary>
        public const string MimicSwap = "ecp_mimic_swap";

        /// <summary>RPC from the opener to the mimic owner.</summary>
        public const string MimicOpen = "ecp_mimic_open";

        /// <summary>Global key prefix of a mountain whose Rime Giant has come; the region index follows.</summary>
        public const string RimeGiantClaimPrefix = "ecp_rimegiant_";

        /// <summary>Plates left on a Rime Giant.</summary>
        public const string RimePlates = "ecp_rime_plates";

        /// <summary>Burial chamber already rolled for an Executioner.</summary>
        public const string HeadsmanChamber = "ecp_hs_chamber";

        /// <summary>Kraken fight state.</summary>
        public const string KrakenPhase = "ecp_kraken_phase";
    }
}
