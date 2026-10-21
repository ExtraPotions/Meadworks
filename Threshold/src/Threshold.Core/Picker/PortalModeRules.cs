// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Portals;
namespace ExtraPotions.Threshold.Picker
{
    public static class PortalModeRules
    {
        public static bool Routes(TeleportMode mode) => mode == TeleportMode.Map || mode == TeleportMode.TargetTeleport;
        public static bool CanEnter(TeleportMode mode, PortalEntry portal) => Routes(mode) && (mode != TeleportMode.TargetTeleport || portal.IsTagged);
        public static IReadOnlyList<PickerRow> NamedChoices(PickerQuery query) => PickerList.Build(query).SelectMany(group => group.Rows)
            .Where(row => row.Portal.IsTagged).OrderByDescending(row => row.IsFavourite)
            .ThenBy(row => row.Portal.Tag.Trim(), StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.Distance).ToArray();
    }
}
