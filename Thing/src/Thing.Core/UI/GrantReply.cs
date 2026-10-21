// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Thing.Admin;

namespace ExtraPotions.Thing.UI
{
    public static class GrantReply
    {
        public static bool IsCurrent(string requestedId, string selectedId) =>
            !string.IsNullOrEmpty(requestedId) && string.Equals(requestedId, selectedId, StringComparison.Ordinal);

        public static bool TryRead(string requestedId, string selectedId, IReadOnlyList<string> lines, out AdminRight set)
        {
            set = AdminRight.None;
            if (!IsCurrent(requestedId, selectedId) || lines == null) return false;
            foreach (var line in lines)
                if (line != null && line.StartsWith("GRANTS\t", StringComparison.Ordinal))
                    return AdminPermissions.TryWire(line, out _, out set);
            return false;
        }
    }
}
