// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Thing registers the admin window with Crucible's Meadworks menu (MeadworksMenu: F8, or the
// single "Meadworks" pause-menu entry). The admin check alone decides whether the entry is listed.
using System;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingPanelMenu
    {
        internal const string Id = "thing";
        static IDisposable? registration;

        internal static void Tick()
        {
            if (!Plugin.Ready || !Plugin.AllowsUi) { Reset(); return; }
            registration ??= MeadworksMenu.Register(Id, "Thing", ThingPanel.Open, Listed);
        }

        /// <summary>Listed while the local player may administer.</summary>
        internal static bool Listed() => ThingPanel.CanOpen;

        internal static void Reset()
        {
            registration?.Dispose();
            registration = null;
        }
    }
}
