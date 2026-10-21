// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room (Max_Dungeon_Rooms is a behavior reference only; no source read): room counts are the
// generator's own m_minRooms and m_maxRooms, set before it places rooms and restored after (design A1 to A5).
using System;
using System.IO;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Integration;
using ExtraPotions.Delve.Rooms;
using HarmonyLib;

namespace ExtraPotions.Delve.Runtime
{
    public static class DelveRooms
    {
        public const string FileName = "Delve.Rooms.yml";

        static RoomsFile file = RoomsFile.Empty;
        static DateTime fileStamp = DateTime.MinValue;
        static bool reported;

        /// <summary>The counts for a generator now, or null when Delve leaves it alone.</summary>
        public static RoomCounts? For(string generator)
        {
            var s = Plugin.Settings;
            if (s == null || !DelveModules.Active(DelveModule.Rooms)) return null;
            var settings = new RoomSettings(s.Get(DelveSettings.Rooms.Enabled), s.Get(DelveSettings.Rooms.MinRooms), s.Get(DelveSettings.Rooms.MaxRooms),
                new RoomOverride(s.Get(DelveSettings.Rooms.ForestCryptOverride), s.Get(DelveSettings.Rooms.ForestCryptMin), s.Get(DelveSettings.Rooms.ForestCryptMax)),
                new RoomOverride(s.Get(DelveSettings.Rooms.SunkenCryptOverride), s.Get(DelveSettings.Rooms.SunkenCryptMin), s.Get(DelveSettings.Rooms.SunkenCryptMax)),
                new RoomOverride(s.Get(DelveSettings.Rooms.CaveOverride), s.Get(DelveSettings.Rooms.CaveMin), s.Get(DelveSettings.Rooms.CaveMax)));
            return RoomRules.Effective(generator, settings, File());
        }

        /// <summary>Delve.Rooms.yml, read again whenever it changes on disk; its errors are logged once per change.</summary>
        static RoomsFile File()
        {
            try
            {
                var path = Path.Combine(DelveImport.DataDirectory, FileName);
                if (!System.IO.File.Exists(path)) return file = RoomsFile.Empty;
                var stamp = System.IO.File.GetLastWriteTimeUtc(path);
                if (stamp == fileStamp) return file;
                fileStamp = stamp;
                file = RoomsFile.Parse(System.IO.File.ReadAllText(path));
                foreach (var error in file.Errors) Plugin.Log?.Warning(FileName + " " + error);
                reported = false;
                return file;
            }
            catch (IOException error)
            {
                Plugin.Log?.Warning(FileName + " unreadable: " + error.Message);
                return file;
            }
        }

        /// <summary>Generators named in the file that the game does not know, logged once after the prefabs exist.</summary>
        public static void ReportUnknown()
        {
            if (reported || ZNetScene.instance == null) return;
            reported = true;
            var known = new System.Collections.Generic.List<string>();
            foreach (var prefab in ZNetScene.instance.m_prefabs)
                if (prefab != null && prefab.GetComponent<DungeonGenerator>() != null) known.Add(prefab.name);
            foreach (var name in RoomRules.Unknown(File(), known))
                Plugin.Log?.Info(FileName + ": " + name + " is not a dungeon generator in this game; ignored.");
        }

        public static void Reset()
        {
            file = RoomsFile.Empty;
            fileStamp = DateTime.MinValue;
            reported = false;
        }
    }

    /// <summary>Sizes a generator with the Dungeon algorithm before it places rooms (GenerateRooms is synchronous) and restores its own values after.</summary>
    [HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.GenerateRooms))]
    public static class GenerateRoomsPatch
    {
        static void Prefix(DungeonGenerator __instance, out (int Min, int Max)? __state)
        {
            __state = null;
            try
            {
                if (__instance == null || __instance.m_algorithm != DungeonGenerator.Algorithm.Dungeon) return;
                var counts = DelveRooms.For(Utils.GetPrefabName(__instance.gameObject));
                if (counts == null) return;
                __state = (__instance.m_minRooms, __instance.m_maxRooms);
                __instance.m_minRooms = counts.Value.Min;
                __instance.m_maxRooms = counts.Value.Max;
            }
            catch (Exception error)
            {
                Plugin.Log?.Warning("Room counts skipped: " + error.GetType().Name + ": " + error.Message);
            }
        }

        /// <summary>A finalizer, so the generator gets its own values back even when placement throws.</summary>
        static void Finalizer(DungeonGenerator __instance, (int Min, int Max)? __state)
        {
            if (__state == null || __instance == null) return;
            __instance.m_minRooms = __state.Value.Min;
            __instance.m_maxRooms = __state.Value.Max;
        }
    }
}
