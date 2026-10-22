// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Replaces only the biome ambient track. Combat, bosses, locations, sailing and home still win.
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Audio;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Config;
using HarmonyLib;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Seasonal choice among MusicMan's own entries. The catalog is read when the season or the list changes, not every frame.
    /// A headless server does not touch the mixer.
    /// </summary>
    public static class SeasonMusicRuntime
    {
        static MusicTrack[] buffer = Array.Empty<MusicTrack>();
        static int[] source = Array.Empty<int>();
        static int cachedSeason = int.MinValue, cachedCount = -1;
        static string? cachedAmbient;
        static MusicMan.NamedMusic? chosen;
        static string? playing;

        public static void Invalidate()
        {
            long started = SeasonFrame.Stamp();
            cachedSeason = int.MinValue;
            if (!Wanted()) Release();
            SeasonFrame.Add(SeasonFrameSystem.MusicInvalidate, started);
        }

        public static void Shutdown()
        {
            Release();
            cachedSeason = int.MinValue;
            cachedAmbient = null;
            chosen = null;
            buffer = Array.Empty<MusicTrack>();
            source = Array.Empty<int>();
        }

        public static string Describe() => playing != null ? " music " + playing : "";

        public static void Apply(MusicMan music, ref MusicMan.NamedMusic result)
        {
            if (!MeadworksHeadless.AllowsUi || music == null || result == null || !Wanted()) return;
            var view = SolsticeSeasons.View;
            if (!view.Available || view.Season < 0 || view.Season >= Seasons.Count) return;
            var env = EnvMan.instance;
            string? ambient = env != null ? env.GetAmbientMusic() : null;
            if (string.IsNullOrEmpty(ambient) || !string.Equals(result.m_name, ambient, StringComparison.Ordinal)) return;
            if (SeasonMusic.IsReserved(ambient)) return;
            var pick = Choose(music, (Season)view.Season, ambient);
            if (pick == null || pick == result) return;
            result = pick;
            playing = pick.m_name;
        }

        static bool Wanted()
        {
            var settings = Plugin.Settings;
            return settings != null && settings.Get(SolsticeSettings.Music.SeasonalMusic);
        }

        static MusicMan.NamedMusic? Choose(MusicMan music, Season season, string ambient)
        {
            var list = music.m_music;
            int count = list != null ? list.Count : 0;
            if (cachedSeason == (int)season && cachedCount == count && string.Equals(cachedAmbient, ambient, StringComparison.Ordinal))
                return chosen;
            cachedSeason = (int)season;
            cachedCount = count;
            cachedAmbient = ambient;
            chosen = null;
            if (list == null || count == 0) return null;
            if (buffer.Length < count)
            {
                int n = buffer.Length == 0 ? 16 : buffer.Length;
                while (n < count) n *= 2;
                buffer = new MusicTrack[n];
                source = new int[n];
            }
            int filled = 0;
            for (int i = 0; i < count; i++)
            {
                var entry = list[i];
                if (entry == null || string.IsNullOrEmpty(entry.m_name)) continue;
                bool playable = entry.m_enabled && entry.m_clips != null && entry.m_clips.Length > 0;
                buffer[filled] = new MusicTrack(entry.m_name, entry.m_ambientMusic, playable);
                source[filled] = i;
                filled++;
            }
            int index = SeasonMusic.Select(season, ambient, buffer, filled);
            if (index < 0) return null;
            chosen = list[source[index]];
            return chosen;
        }

        static void Release()
        {
            var name = playing;
            playing = null;
            if (!MeadworksHeadless.AllowsUi || name == null) return;
            var music = MusicMan.instance;
            if (music != null && music.GetCurrentMusic() == name) music.StopMusic();
        }
    }

    [HarmonyPatch(typeof(MusicMan), nameof(MusicMan.GetEnvironmentMusic))]
    static class SeasonMusicPatch
    {
        static void Postfix(MusicMan __instance, ref MusicMan.NamedMusic __result) =>
            SeasonMusicRuntime.Apply(__instance, ref __result);
    }
}
