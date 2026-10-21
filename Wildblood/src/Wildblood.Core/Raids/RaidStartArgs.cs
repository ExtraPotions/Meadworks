// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Wildblood.Raids
{
    public enum RaidStartParse { Ok, Usage, BadTier }

    /// <summary>
    /// <c>wildblood_raid start &lt;coins&gt; [tier] [request-id]</c>. A tier (0-<see cref="RaidTiers.Top"/>) weighs the stake
    /// against that gear tier instead of the strongest player near the chest; without one the tier is measured. A lone
    /// third word of at most three digits is the tier, anything else is the request ID (a request ID is 32 hex characters).
    /// </summary>
    public readonly struct RaidStartArgs
    {
        /// <summary>The tier word's longest form; a longer all-digit word is a request ID.</summary>
        const int TierDigits = 3;

        RaidStartArgs(int coins, int tier, string? requestId)
        {
            Coins = coins;
            Tier = tier;
            RequestId = requestId;
        }

        public int Coins { get; }

        /// <summary>The named tier, or -1 to measure it.</summary>
        public int Tier { get; }

        /// <summary>The named request ID, or null for a new one.</summary>
        public string? RequestId { get; }

        /// <summary><paramref name="args"/> as the game passes them, the command name first. <paramref name="badTier"/> is the
        /// word that named no tier when the result is <see cref="RaidStartParse.BadTier"/>.</summary>
        public static RaidStartParse Parse(IReadOnlyList<string>? args, out RaidStartArgs parsed, out string badTier)
        {
            parsed = default;
            badTier = "";
            if (args == null || args.Count < 3 || args.Count > 5 || args[1] != "start"
                || !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out int coins))
                return RaidStartParse.Usage;
            int tier = -1;
            string? request = null;
            if (args.Count == 5 || args.Count == 4 && LooksLikeTier(args[3]))
            {
                if (!TryTier(args[3], out tier))
                {
                    badTier = args[3];
                    return RaidStartParse.BadTier;
                }
                if (args.Count == 5) request = args[4];
            }
            else if (args.Count == 4)
            {
                request = args[3];
            }
            parsed = new RaidStartArgs(coins, tier, request);
            return RaidStartParse.Ok;
        }

        static bool LooksLikeTier(string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length > TierDigits) return false;
            foreach (char c in word)
                if (c < '0' || c > '9') return false;
            return true;
        }

        static bool TryTier(string word, out int tier)
        {
            if (LooksLikeTier(word) && int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out tier) && tier <= RaidTiers.Top)
                return true;
            tier = -1;
            return false;
        }
    }
}
