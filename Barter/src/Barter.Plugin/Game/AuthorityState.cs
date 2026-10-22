// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;
using ExtraPotions.Barter.Trading;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Barter.Runtime
{
    internal static class AuthorityState
    {
        private static AuthenticatedStateStore store;
        private static World storedWorld;
        private static string storedPath;
        private static FileHelpers.FileSource storedSource;
        internal static bool IsAuthority => ZNet.instance != null && ZNet.instance.IsServer();
        private static string ObjectId(ZDO zdo) => zdo.m_uid.UserID.ToString(CultureInfo.InvariantCulture) + ":" + zdo.m_uid.ID.ToString(CultureInfo.InvariantCulture);

        private static AuthenticatedStateStore Store
        {
            get
            {
                World world = ZNet.World;
                if (world == null) throw new InvalidOperationException("Authority world save is unavailable");
                string path = world.IsChunkedSave() ? Path.Combine(world.GetSaveDirectory(world.m_fileSource), "Barter.Authority") : world.GetDBPath() + ".Barter.Authority";
                if (store == null || storedWorld != world || storedPath != path || storedSource != world.m_fileSource)
                {
                    var candidate = new AuthenticatedStateStore(path, world.m_fileSource == FileHelpers.FileSource.Cloud ? new WorldAuthorityStorage(world.m_fileSource) : null,
                        world.m_needsDB || FileHelpers.Exists(world.GetDBPath(), world.m_fileSource));
                    if (store != null && storedWorld == world)
                        candidate.ImportFrom(CruciblePlugin.Session.WorldId, store, replaceExisting: true);
                    else if (world.IsChunkedSave())
                        candidate.ImportFrom(CruciblePlugin.Session.WorldId, new AuthenticatedStateStore(world.GetDBPath() + ".Barter.Authority",
                            world.m_fileSource == FileHelpers.FileSource.Cloud ? new WorldAuthorityStorage(world.m_fileSource) : null));
                    candidate.ImportLocal(CruciblePlugin.Session.WorldId, Path.Combine(BepInEx.Paths.ConfigPath, "Barter.Authority"));
                    store = candidate; storedWorld = world; storedPath = path; storedSource = world.m_fileSource;
                }
                return store;
            }
        }

        internal static string Read(ZDO zdo, string field, string displayMirror = "")
        {
            if (!IsAuthority) return displayMirror;
            if (zdo == null || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady)
                throw new InvalidOperationException("Authority state is unavailable");
            zdo.SetOwner(ZDOMan.GetSessionID());
            return Store.Read(CruciblePlugin.Session.WorldId, ObjectId(zdo), field);
        }

        internal static void Write(ZDO zdo, string field, string value)
        {
            if (!IsAuthority || zdo == null || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady)
                throw new InvalidOperationException("Only the ready authority writes trader state");
            zdo.SetOwner(ZDOMan.GetSessionID());
            Store.Write(CruciblePlugin.Session.WorldId, ObjectId(zdo), field, value);
        }

        internal static string[] Fields(ZDO zdo, string prefix)
        {
            if (!IsAuthority || zdo == null || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady) throw new InvalidOperationException("Authority state is unavailable");
            return Store.Fields(CruciblePlugin.Session.WorldId, ObjectId(zdo), prefix);
        }

        internal static void Remove(ZDO zdo, string field)
        {
            if (!IsAuthority || zdo == null || !CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady) throw new InvalidOperationException("Authority state is unavailable");
            Store.Remove(CruciblePlugin.Session.WorldId, ObjectId(zdo), field);
        }
    }
}
