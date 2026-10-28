// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Bestiary
{
    /// <summary>Bestiary.Core identity: Unity-free logic shared by the plugin and the portable tests.</summary>
    public static class BestiaryCore
    {
        public const string ProductId = "com.meadworks.bestiary";
        public const string ProductName = "Bestiary";
        public const string Family = "Fang";
        public const string Version = "0.1.23";

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

        /// <summary>Global key prefix of an Ashlands region whose Slag Charred has come. Delve's claim reset clears it.</summary>
        public const string SlagCharredClaimPrefix = "mw_slagcharred_";

        /// <summary>Plates left on a Slag Charred.</summary>
        public const string SlagPlates = "mw_slag_plates";

        /// <summary>Fire already counted toward the next plate. Kept so a reload does not drop a partial plate.</summary>
        public const string SlagFire = "mw_slag_fire";

        /// <summary>Set after a Fuling Spearman has thrown its one spear.</summary>
        public const string JavelinThrown = "mw_javelin_thrown";

        /// <summary>World-clock ticks when a Draugr Hook may throw again. The owner writes it.</summary>
        public const string HookReady = "mw_hook_ready";

        /// <summary>Asks the owner of a character to be pulled toward a hook.</summary>
        public const string BogHookPull = "MW_BogHookPull";

        /// <summary>A zone owner asks the server to claim one Ashlands region before a Slag Charred spawns.</summary>
        public const string SlagClaimAsk = "MW_SlagClaim";

        /// <summary>The server's answer to <see cref="SlagClaimAsk"/>.</summary>
        public const string SlagClaimAnswer = "MW_SlagClaimAnswer";

        /// <summary>A Delve reset clears Rime Giant mountains and Slag Charred regions. Marrow Lanterns have no claim.</summary>
        public static bool IsRegionClaim(string key) =>
            !string.IsNullOrEmpty(key)
            && (key.StartsWith(RimeGiantClaimPrefix, StringComparison.Ordinal)
                || key.StartsWith(SlagCharredClaimPrefix, StringComparison.Ordinal));

        /// <summary>Burial chamber already rolled for an Executioner.</summary>
        public const string HeadsmanChamber = "ecp_hs_chamber";

        /// <summary>Kraken fight state.</summary>
        public const string KrakenPhase = "ecp_kraken_phase";
    }
}
