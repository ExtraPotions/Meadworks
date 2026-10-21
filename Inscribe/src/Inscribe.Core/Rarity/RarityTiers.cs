// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the five shown rarity tiers over the stored ids, socket odds per tier and the Legendary source gate.
using System;

namespace ExtraPotions.Inscribe.Rarity
{
    /// <summary>
    /// Shown tiers over stored ids (batch 3): normal is Common, magic Uncommon, rare Rare, <see cref="Epic"/> Epic and
    /// <see cref="Legendary"/> Legendary. The stored ids never change; the two new ids carry the <c>mw_</c> prefix so an
    /// older Inscribe, whose migration turns a bare <c>epic</c> or <c>legendary</c> into <c>rare</c>, keeps them as an
    /// unknown rarity instead (inscriptions kept and applied, runes refused, name shown grey).
    /// </summary>
    public static class RarityTiers
    {
        public const string Epic = "mw_epic";
        public const string Legendary = "mw_legendary";

        /// <summary>Stored ids from the lowest tier to the highest.</summary>
        public static readonly string[] Ids = { "normal", "magic", "rare", Epic, Legendary };

        private static readonly float[] UncommonSockets = { 80f, 15f, 4f, 1f };
        private static readonly float[] RareSockets = { 60f, 25f, 11f, 4f };
        private static readonly float[] EpicSockets = { 35f, 35f, 22f, 8f };
        private static readonly float[] LegendarySockets = { 0f, 40f, 40f, 20f };

        /// <summary>
        /// Weights for 0, 1, 2 and 3 sockets on a dropped item of this rarity (Uncommon and Rare are EliteCrafting 0.10.0's).
        /// An unknown id and the base rarity use Uncommon's, as the chisel always did for Normal items.
        /// </summary>
        public static float[] SocketWeights(string? rarityId) => rarityId switch
        {
            "rare" => RareSockets,
            Epic => EpicSockets,
            Legendary => LegendarySockets,
            _ => UncommonSockets,
        };
    }

    /// <summary>Where a Legendary may come from: Wildblood boss kills and raid wins, each with its own switch and chance.</summary>
    public static class LegendarySources
    {
        public const string Boss = "boss";
        public const string Raid = "raid";

        /// <summary>The capability Wildblood calls: <c>Func&lt;string source, string creaturePrefab, Vector3 position, bool&gt;</c>.</summary>
        public const string Capability = "meadworks.inscribe.legendary_reward";
        public static readonly Version Version = new Version(1, 0);

        /// <summary>Whether this source may hand out a Legendary at all; any other source (crafting, drops, runes) never.</summary>
        public static bool Allows(string? source, bool bosses, bool raids) =>
            string.Equals(source, Boss, StringComparison.Ordinal) ? bosses
            : string.Equals(source, Raid, StringComparison.Ordinal) && raids;

        /// <summary>The chance roll: <paramref name="unit"/> in [0, 1) under <paramref name="percent"/>.</summary>
        public static bool Rolls(float percent, double unit) => percent > 0f && unit * 100.0 < percent;
    }

    /// <summary>
    /// Whether a gem comes back when it is replaced or its item is salvaged (batch 3): a host chance per tier, Common
    /// highest. Defaults set by the user: Common 100, Uncommon 90, Rare 75, Epic 50, Legendary 25 percent. A gem that does
    /// not come back is lost, as before.
    /// </summary>
    public static class GemRecovery
    {
        public static readonly float[] Defaults = { 100f, 90f, 75f, 50f, 25f };

        /// <summary>The tier of a stored rarity id, 0 Common to 4 Legendary; an id the ladder does not know counts as Legendary.</summary>
        public static int TierOf(string? rarityId)
        {
            if (rarityId == null) return 0;
            int index = Array.IndexOf(RarityTiers.Ids, rarityId);
            return index >= 0 ? index : RarityTiers.Ids.Length - 1;
        }

        /// <summary>The percent for this rarity from the five host settings, clamped to 0-100.</summary>
        public static float PercentFor(string? rarityId, float[] percents) =>
            Math.Max(0f, Math.Min(100f, percents[Math.Min(TierOf(rarityId), percents.Length - 1)]));

        /// <summary>The roll: <paramref name="unit"/> in [0, 1) under <paramref name="percent"/>; 100 always, 0 never.</summary>
        public static bool Returns(float percent, double unit) => percent >= 100f || (percent > 0f && unit * 100.0 < percent);
    }
}
