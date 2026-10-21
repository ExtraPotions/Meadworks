// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Hex.Runtime.Native;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;

namespace ExtraPotions.Hex.Runtime;

internal static partial class HexWindow
{
    static void SpawnProtectionRow(List<KitVirtualRow> rows)
    {
        const string key = "spawn_protection";
        rows.Add(new KitVirtualRow(key, 100, rect =>
        {
            cleanup[rect] = new List<Action>();
            var title = HexText.Text("spawn_protection_label");
            KitAlmanacDialog.Label(rect, "Label", title, Vector2.zero, new Vector2(350, 35), 16);
            KitAlmanacDialog.Label(rect, "Legend", HexText.Text("spawn_protection_legend"), new Vector2(0, -40), new Vector2(708, 55), 14);
            var toggle = KitToggle.Create(rect, "Value", HexSpawnProtection.IsEnabled,
                value => Result(HexSpawnProtection.SetEnabled(value)), new Vector2(380, 0), new Vector2(140, 32), Ui("on"), Ui("off"));
            toggle.Tooltip(title, HexText.Text("spawn_protection_legend"));
            cleanup[rect].Add(toggle.Dispose);
            updates[rect] = () =>
            {
                toggle.SetValue(HexSpawnProtection.IsEnabled);
                var status = MeadworksMischief.Query(Plugin.Guid, "SpawnProtection", MischiefCategory.Information);
                toggle.SetUsable(Plugin.Settings!.Enabled.Value && Plugin.Settings.DebugTools.Value
                    && HexMovement.Owned(Player.m_localPlayer) && status.Allowed, status.Allowed ? "" : MeadworksBlockedOptions.Reason(status.Reason));
            };
            updates[rect]();
            Focus(rect, key);
        }, Unbind));
    }
}
