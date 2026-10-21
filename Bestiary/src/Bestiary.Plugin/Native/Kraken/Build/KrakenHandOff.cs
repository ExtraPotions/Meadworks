// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Bootstrap;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// What the kraken shares with Elite Creatures Reborn when that mod is installed: key names and a version, as for the
    /// mimic (<see cref="Mimic.EliteHandOff"/>); neither mod references the other. That mod rolls stars and a boss aspect
    /// onto the kraken like any boss, and from 3.12.0 its own rules leave Twin and Phantom out of the kraken's rotation
    /// (a Twin puts two krakens on one hull; Phantom copies would swarm the deck). An older version does not know the
    /// kraken, so against one the kraken is marked rolled, with nothing, the moment it comes into the world, and that
    /// mod leaves it alone. Without that mod nothing here is read or written.
    /// </summary>
    internal static class KrakenHandOff
    {
        public const string RolledKey = "ecr_resolved";
        private const string EliteGuid = "gglasgow.elitecreaturesreborn";
        private static readonly System.Version Knows = new System.Version(3, 11, 1);
        private static bool? olderElite;

        /// <summary>
        /// OWNER, as a new kraken wakes (before that mod's first look at it, which waits for Start): against a version that
        /// does not know the kraken, marked rolled so it gets no aspect at all.
        /// </summary>
        public static void Guard(ZNetView nview)
        {
            ZDO? zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            if (zdo != null && nview!.IsOwner() && OlderElite && !zdo.GetBool(RolledKey))
            {
                zdo.Set(RolledKey, true);
            }
        }

        // Whether Elite Creatures Reborn is installed at a version from before its rules knew the kraken.
        private static bool OlderElite => olderElite ??=
            Chainloader.PluginInfos.TryGetValue(EliteGuid, out BepInEx.PluginInfo info) && info.Metadata.Version < Knows;
    }
}
