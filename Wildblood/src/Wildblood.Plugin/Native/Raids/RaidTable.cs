// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace, authority-owned raid integration and the Deep North raid tier 7.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// Every fixed number of the raids (features/raids.md sections 3 to 6): the fair stake per gear tier, the heat bands
    /// and the clock, ranges and limits. The spec settles that none of these is a setting - the only one is the Raiders
    /// Chest's on/off - so tuning happens here. The numbers are the spec's judgement calls, to be argued with in play.
    /// </summary>
    public static class RaidTable
    {
        // ---- The fight's shape (section 4). ----

        /// <summary>Waves in every raid; the last is led by the Warlord.</summary>
        public const int Waves = 3;

        /// <summary>Seconds from sounding the raid to the first wave.</summary>
        public const float CountdownSeconds = 20f;

        /// <summary>Seconds between a wave's last raider falling and the next wave.</summary>
        public const float BreakSeconds = 20f;

        /// <summary>Of each creature's count in the game's spawn list, the share each wave brings: half, two thirds, all.</summary>
        public static readonly float[] WaveShares = { 0.5f, 2f / 3f, 1f };

        /// <summary>A quarter more raiders for each player near the raid past the first.</summary>
        public const float ExtraPerPlayer = 0.25f;

        /// <summary>The most raiders alive at once; the rest arrive as others die.</summary>
        public const int MaxAlive = 20;

        /// <summary>Metres from the host to the nearest point a wave may arrive at.</summary>
        public const float SpawnRingMin = 40f;

        /// <summary>Metres from the host to the furthest point a wave may arrive at.</summary>
        public const float SpawnRingMax = 60f;

        /// <summary>Stars the Warlord has on top of its band's extra stars.</summary>
        public const int WarlordExtraStars = 2;

        /// <summary>The Warlord's share of every coin the raiders carry.</summary>
        public const float WarlordCoinShare = 0.25f;

        /// <summary>No raider is given more stars than this (the most the mod's own Extreme difficulty draws).</summary>
        public const int MaxRaiderStars = 8;

        // ---- How it ends (section 4). ----

        /// <summary>Seconds from the start to a timed-out end: 15 minutes.</summary>
        public const float TimeLimitSeconds = 15f * 60f;

        /// <summary>Seconds with no player within <see cref="RaidRadius"/> before the raid is abandoned.</summary>
        public const float AbandonSeconds = 60f;

        // ---- Ranges (sections 2 to 5). ----

        /// <summary>Metres around the host that are the raid: whose players count for the heat, see the HUD line and
        /// keep the raid alive, and within which plunderers look for chests and stations. The game's own raid size.</summary>
        public const float RaidRadius = 96f;

        /// <summary>Metres from the host within which players read the raid's messages.</summary>
        public const float MessageRange = 100f;

        /// <summary>Metres within which Raiders Chests share one cooldown.</summary>
        public const float CooldownShareRange = 100f;

        /// <summary>Metres between raids: none starts this close to another raid, the game's or a chest's.</summary>
        public const float RaidSpacing = 200f;

        /// <summary>In-game days from one raid's start to the next at the same chest.</summary>
        public const float CooldownDays = 1f;

        // ---- The machinery. ----

        /// <summary>Seconds between the runner's ticks on the host's owner: a few times a second, never every frame.</summary>
        public const float TickSeconds = 0.25f;

        /// <summary>Seconds between stamps of "a player was here" in the host's ZDO, so it is not resent every tick.</summary>
        public const float SeenStampSeconds = 5f;

        // ---- Gold against gear (section 3). ----

        /// <summary>The highest gear tier: the Deep North (Bloodgold or one of its armour sets).</summary>
        public const int MaxTier = global::ExtraPotions.Wildblood.Raids.RaidTiers.Top;

        /// <summary>The heat bands, coolest first. Raiders "fewer" in Trivial is 0.6 of the count; "nearly all" mutated in
        /// Deadly is 90 in 100.</summary>
        private static readonly RaidBand[] Bands =
        {
            new RaidBand(0, "Trivial", 0f, 0.5f, 0.6f, 0, 0f, 1f, 0.1f, 0.5f),
            new RaidBand(1, "Fair", 0.5f, 1.5f, 1f, 0, 0f, 1.1f, 1f, 1f),
            new RaidBand(2, "Hard", 1.5f, 3f, 1.5f, 1, 100f / 3f, 1.25f, 2f, 2f),
            new RaidBand(3, "Brutal", 3f, 6f, 2f, 2, 50f, 1.5f, 3.5f, 3.5f),
            new RaidBand(4, "Deadly", 6f, float.PositiveInfinity, 2.5f, 3, 90f, 2f, 5f, 5f),
        };

        /// <summary>The fair stake for a gear tier, clamped to 0-7: leather 100, bronze 200, iron 400, silver 600, black
        /// metal 900, Mistlands 1300, Flametal 1800, the Deep North 2400 (Wildblood.Core RaidTiers).</summary>
        public static int FairStake(int tier) => global::ExtraPotions.Wildblood.Raids.RaidTiers.FairStake(tier);

        /// <summary>The band a heat falls in.</summary>
        public static RaidBand BandFor(float heat)
        {
            for (int i = Bands.Length - 1; i > 0; i--)
            {
                if (heat >= Bands[i].FromHeat)
                {
                    return Bands[i];
                }
            }
            return Bands[0];
        }

        /// <summary>The band stored as an index in a host's ZDO; Trivial for anything out of range.</summary>
        public static RaidBand Band(int index) => Bands[index >= 0 && index < Bands.Length ? index : 0];
    }
}
