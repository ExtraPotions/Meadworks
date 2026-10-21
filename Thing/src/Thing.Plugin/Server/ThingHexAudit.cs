// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Moderation;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingHexAudit
    {
        internal static void Initialize(ProductRegistration registration) =>
            registration.AddCapability<Action<string, string, string, int, int, int, bool, bool>>(
                HexSpawnAudit.CapabilityId, HexSpawnAudit.Version, Write);
        static void Write(string actor, string kind, string prefab, int requested, int created, int level, bool tame, bool accepted)
        {
            var net = ZNet.instance;
            if (!Plugin.Ready || net == null || !net.IsServer() || !ThingServer.Active
                || !CruciblePlugin.Session.AuthorityReady || !ThingModules.ProductActive(out _)
                || !HexSpawnAudit.Valid(actor, kind, prefab, requested, created, level, accepted)) return;
            ThingServer.Audit(new Actor(actor, ""), ThingAction.HexSpawn, default,
                HexSpawnAudit.Outcome(requested, created, accepted),
                HexSpawnAudit.Detail(kind, prefab, requested, created, level, tame));
        }
    }
}
