// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the version of a built portal list (design D1). Same idea as Wayfare's PortalListVersion
// (GPL-3.0-or-later), which hashes ZDO data revisions; this one hashes the fields a client sees, so it is portable.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Threshold.Portals
{
    /// <summary>The list as one number: a client sends the version it holds and the server answers only when its own differs.</summary>
    public static class PortalListVersion
    {
        public static long Of(IReadOnlyList<PortalEntry> portals)
        {
            if (portals == null) throw new ArgumentNullException(nameof(portals));
            var hash = ListHash.Start(portals.Count);
            foreach (var p in portals)
            {
                hash.Add(p.Id.User);
                hash.Add(p.Id.Id);
                Add(ref hash, p.Prefab);
                Add(ref hash, p.Tag);
                hash.Add(p.Position);
                hash.Add((int)p.Mode);
                hash.Add(p.Owner);
                Add(ref hash, p.HubName);
                hash.Add(p.AllowsAllItems ? 1 : 0);
                hash.Add(p.Yaw);
            }
            return hash.Value;
        }

        /// <summary>Length, then each UTF-16 unit, so "ab"+"c" and "a"+"bc" differ.</summary>
        private static void Add(ref ListHash hash, string text)
        {
            hash.Add(text.Length);
            foreach (var c in text) hash.Add((int)c);
        }
    }
}
