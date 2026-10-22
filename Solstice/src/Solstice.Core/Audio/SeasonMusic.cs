// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Picks one of the game's own ambient tracks. Combat, bosses and locations stay reserved.
using System;
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Audio
{
    /// <summary>One MusicMan entry, as the client read it. The name is the game's.</summary>
    public readonly struct MusicTrack
    {
        public MusicTrack(string? name, bool ambient, bool enabled)
        {
            Name = name ?? "";
            Ambient = ambient;
            Enabled = enabled;
        }

        public string Name { get; }
        public bool Ambient { get; }
        public bool Enabled { get; }
    }

    /// <summary>
    /// Chooses a seasonal ambient track from names the game already has. A reserved name (combat, a boss, a location,
    /// sailing, home, morning, evening) is never returned, and a reserved environment track is left alone.
    /// </summary>
    public static class SeasonMusic
    {
        static readonly string[] Exact =
        {
            "combat", "intro", "respawn", "home", "sailing", "sailing_ashlands", "morning", "evening", "menu", "wakeup"
        };

        static readonly string[] Fragments =
        {
            "boss", "combat", "location", "crypt", "dungeon", "cave", "mine", "event", "raid", "sailing",
            "queen", "fader", "eikthyr", "elder", "bonemass", "moder", "yagluth", "gdking", "goblinking", "dragon", "frozenking", "hive",
            "battle", "cottage", "ruins", "fortress", "village", "hildir", "haldor", "tower", "sanctum", "memorial",
            "witch", "camp", "music_"
        };

        static readonly string[] SpringKeys = { "meadow", "blackforest", "black forest" };
        static readonly string[] SummerKeys = { "plains", "meadow", "blackforest", "black forest" };
        static readonly string[] FallKeys = { "blackforest", "black forest", "swamp", "mountain" };
        static readonly string[] WinterKeys = { "deepnorth", "deep north", "mountain", "swamp", "mistland", "ocean" };

        public static bool IsReserved(string? name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            for (int i = 0; i < Exact.Length; i++)
                if (string.Equals(name, Exact[i], StringComparison.OrdinalIgnoreCase)) return true;
            for (int i = 0; i < Fragments.Length; i++)
                if (name.IndexOf(Fragments[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>Index into <paramref name="tracks"/>, or -1 to keep the environment track.</summary>
        public static int Select(Season season, string? environmentName, MusicTrack[]? tracks, int count)
        {
            if (IsReserved(environmentName) || tracks == null || count <= 0) return -1;
            var keys = Keys(season);
            int best = -1, bestKey = int.MaxValue, bestRank = int.MinValue;
            int limit = count < tracks.Length ? count : tracks.Length;
            for (int i = 0; i < limit; i++)
            {
                var track = tracks[i];
                if (!track.Enabled || track.Name.Length == 0 || IsReserved(track.Name)) continue;
                int key = KeyIndex(keys, track.Name);
                if (key < 0) continue;
                int rank = Rank(season, track);
                if (best >= 0 && (key > bestKey || (key == bestKey && (rank < bestRank
                    || (rank == bestRank && string.Compare(track.Name, tracks[best].Name, StringComparison.OrdinalIgnoreCase) >= 0)))))
                    continue;
                best = i;
                bestKey = key;
                bestRank = rank;
            }
            if (best < 0) return -1;
            return string.Equals(tracks[best].Name, environmentName, StringComparison.OrdinalIgnoreCase) ? -1 : best;
        }

        static string[] Keys(Season season)
        {
            switch (season)
            {
                case Season.Spring: return SpringKeys;
                case Season.Summer: return SummerKeys;
                case Season.Fall: return FallKeys;
                default: return WinterKeys;
            }
        }

        static int KeyIndex(string[] keys, string name)
        {
            for (int i = 0; i < keys.Length; i++)
                if (name.IndexOf(keys[i], StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return -1;
        }

        /// <summary>Winter prefers a night track. Spring and summer prefer a day track. An ambient flag wins a tie.</summary>
        static int Rank(Season season, MusicTrack track)
        {
            bool night = track.Name.IndexOf("night", StringComparison.OrdinalIgnoreCase) >= 0;
            int rank = season == Season.Winter ? (night ? 2 : 0) : season == Season.Fall ? 0 : (night ? 0 : 2);
            if (track.Ambient) rank++;
            return rank;
        }
    }
}
