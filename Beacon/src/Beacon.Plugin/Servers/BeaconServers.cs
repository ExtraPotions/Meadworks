// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the servers.json file (design 4.1). It never holds a password.
using System;
using System.IO;
using System.Text;
using BepInEx;
using ExtraPotions.Beacon.Servers;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Beacon.Runtime
{
    public static class BeaconServers
    {
        /// <summary>A file Beacon could not read is never overwritten (the player's list is kept for repair).</summary>
        static bool readOnly;

        public static ServerList List { get; private set; } = new ServerList();

        public static string DataDirectory => Path.Combine(Paths.ConfigPath, BeaconCore.DataFolder.Replace('/', Path.DirectorySeparatorChar));

        public static string FilePath => Path.Combine(DataDirectory, BeaconCore.ServerListFile);
        public static bool Writable => !readOnly;

        public static void Load(MeadworksLog log)
        {
            readOnly = false;
            List = new ServerList();
            if (!File.Exists(FilePath)) return;
            try
            {
                List = ServerListCodec.Read(File.ReadAllText(FilePath, Encoding.UTF8));
                log.Verbose("Beacon server list: " + List.Count + " servers.");
            }
            catch (Exception error) when (error is FormatException || error is IOException || error is UnauthorizedAccessException)
            {
                readOnly = true;
                log.Warning("Beacon could not read " + BeaconCore.ServerListFile + " (" + error.GetType().Name + "); it is left unchanged and nothing is saved this session.");
            }
        }

        public static bool Save(MeadworksLog log)
        {
            if (readOnly)
            {
                log.Warning("Beacon did not save " + BeaconCore.ServerListFile + ": the file on disk could not be read at start.");
                return false;
            }
            WriteAtomically(FilePath, ServerListCodec.Write(List));
            return true;
        }

        public static void Unload()
        {
            List = new ServerList();
            readOnly = false;
        }

        /// <summary>The UI validates a candidate and commits it before replacing the live list.</summary>
        public static bool Amend(Action<ServerList> change, MeadworksLog log)
        {
            if (readOnly) return false;
            var candidate = ServerListCodec.Read(ServerListCodec.Write(List));
            change(candidate);
            try { WriteAtomically(FilePath, ServerListCodec.Write(candidate)); }
            catch (Exception error) { log.Warning("Beacon server list was not changed: " + error.GetType().Name); return false; }
            List = candidate;
            return true;
        }

        /// <summary>Writes through a temporary file in the same folder and replaces the target, so a crash never leaves half a file.</summary>
        public static void WriteAtomically(string path, string text)
        {
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            var temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
