// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Catalog;

namespace ExtraPotions.Bestiary.Text
{
    /// <summary>
    /// Keys of Bestiary's C05 catalog (<c>assets/localization/&lt;Language&gt;.json</c>, namespace <c>bestiary</c>, tokens
    /// <c>$meadworks_bestiary_&lt;key&gt;</c>). Keys keep ECP's word ids (<c>enemy_ecp_*</c>, <c>item_ecp_*</c>, <c>se_ecp_*</c>),
    /// whose English text is the source (design 13). English is authoritative; a test keeps the catalog and this list equal.
    /// </summary>
    public static class BestiaryTextKeys
    {
        public const string Namespace = "bestiary";

        /// <summary>Attack items: the names shown when a creature's blow lands.</summary>
        public static readonly IReadOnlyList<string> Attacks = new[]
        {
            "item_ecp_cryptmimic_bite", "item_ecp_slinger_shot", "item_ecp_rimegiant_sweep", "item_ecp_rimegiant_slam",
            "item_ecp_rimegiant_throw", "item_ecp_headsman_axe", "item_ecp_skeletoncrossbow",
        };

        /// <summary>The Bone Ballista's words: the piece, its missiles, its hover and its messages (EliteCreaturesPack 0.9.0).</summary>
        public static readonly IReadOnlyList<string> Ballista = new[]
        {
            "piece_ecp_boneballista", "piece_ecp_boneballista_description", "item_ecp_bonemissile", "item_ecp_bonemissile_description",
            "hud_ecp_bal_hold", "hud_ecp_bal_letgo", "hud_ecp_bal_loaded", "hud_ecp_bal_empty", "msg_ecp_bal_nomissiles", "msg_ecp_bal_behind",
        };

        public const string Inked = "se_ecp_inked";
        public const string InkedTooltip = "se_ecp_inked_tooltip";

        /// <summary>C09 notice: EliteCreaturesPack installed, Bestiary steps aside (design 11.1).</summary>
        public const string NoticeStepAside = "notice_step_aside";

        /// <summary>C09 notice: CreatureManager has creature files, the workshop steps aside.</summary>
        public const string NoticeWorkshopStepAside = "notice_workshop_step_aside";

        /// <summary>Design 6: macOS client.</summary>
        public const string NoticePlatformUnsupported = "notice_platform_unsupported";

        /// <summary>Design 6: a server without Bestiary; off for the session.</summary>
        public const string NoticeServerMissing = "notice_server_missing";

        public static IReadOnlyList<string> All { get; } = CreatureCatalog.All.Where(c => c.Word != null).Select(c => c.Word!)
            .Concat(Attacks)
            .Concat(Ballista)
            .Concat(CompendiumCatalog.Keys)
            .Concat(ItemCatalog.All.SelectMany(i => new[] { i.Word, Description(i.Word) }))
            .Concat(new[] { Inked, InkedTooltip, NoticeStepAside, NoticeWorkshopStepAside, NoticePlatformUnsupported, NoticeServerMissing })
            .ToArray();

        public static string Description(string itemWord) => itemWord + "_description";

        public static string Token(string key) => "$meadworks_" + Namespace + "_" + key;
    }
}
