// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Thing.Admin
{
    public static class HexSpawnAudit
    {
        public const string CapabilityId = "meadworks.thing.hex_spawn_audit";
        public static readonly Version Version = new Version(1, 0);
        public static bool Valid(string actor, string kind, string prefab, int requested, int created, int level, bool accepted) =>
            Text(actor) && (kind == "item" || kind == "creature") && Text(prefab)
            && requested >= 1 && requested <= 9999 && created >= 0 && created <= requested
            && level >= 1 && (kind == "item" || level <= 100) && (accepted || created == 0);
        static bool Text(string text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 160 && !text.Any(char.IsControl);
        public static AuditOutcome Outcome(int requested, int created, bool accepted) =>
            !accepted ? AuditOutcome.Refused : created == requested ? AuditOutcome.Done : AuditOutcome.Failed;
        public static string Detail(string kind, string prefab, int requested, int created, int level, bool tame) =>
            string.Format(CultureInfo.InvariantCulture, "kind={0}; prefab={1}; requested={2}; created={3}; level={4}; tame={5}",
                kind, prefab, requested, created, level, tame ? "true" : "false");
    }
}
