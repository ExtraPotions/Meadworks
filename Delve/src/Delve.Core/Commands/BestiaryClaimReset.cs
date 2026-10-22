// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a full regenerate asks Bestiary to clear region claims. Bestiary owns the keys.
using System;
using System.Collections.Generic;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Commands
{
    /// <summary>
    /// <c>meadworks.bestiary.world</c> v1. A regenerate of every kind clears Rime Giant and Slag Charred claims.
    /// One kind does not. Bestiary removes the keys; Delve only decides when to ask.
    /// </summary>
    public static class BestiaryClaimReset
    {
        public const string CapabilityId = "meadworks.bestiary.world";
        public static readonly Version Version = new Version(1, 0);

        public static bool Clears(DelveCommand command) =>
            command.Verb == CommandVerb.Regenerate && CoversEveryKind(command.Kinds);

        public static bool CoversEveryKind(IEnumerable<DelveKind> kinds)
        {
            var seen = new HashSet<DelveKind>();
            foreach (var kind in kinds) seen.Add(kind);
            return seen.Count == Enum.GetValues(typeof(DelveKind)).Length;
        }

        public static int Apply(Func<int> reset, bool clear)
        {
            if (!clear || reset == null) return 0;
            return reset();
        }
    }
}
