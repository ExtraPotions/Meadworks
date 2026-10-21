// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: UI observes the existing terrain/drop preview handlers, never copies their tokens or authority logic.
using System;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Thing.Config;
using UnityEngine;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingPanelWorld
    {
        static bool terrain, drops, terrainPreview, dropsPreview;
        internal static void Terrain()
        {
            if (ThingPanel.Window == null) return;
            ThingPanelForms.Open(ThingPanel.Ui("terrain"), "", new[] { ThingPanel.Ui("radius") }, new[] { "5" }, values =>
            {
                if (!int.TryParse(values[0], out var radius) || radius < 1 || radius > 50) throw new ArgumentException();
                ObserveTerrain(); ThingTerrainCommands.Run(new[] { "terrain", radius.ToString() }, fromUi: true);
                return Array.Empty<string>();
            }, false);
        }
        internal static void Pickup()
        {
            if (ThingPanel.Window == null) return;
            var current = ThingPickup.Wanted;
            ThingPanelForms.Open(ThingPanel.Ui("pickup_radius"), ThingPanel.Ui("pickup_radius_hint"), new[] { ThingPanel.Ui("radius") },
                new[] { current.HasValue ? current.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "reset" }, values =>
            {
                if (!ExtraPotions.Thing.WorldAdmin.PickupRadius.TryParse(values[0], out _)) throw new ArgumentException();
                ThingPanel.Message(ThingPickup.Apply(values[0]));
                return Array.Empty<string>();
            }, false);
        }
        static void ObserveTerrain()
        {
            if (terrain) return; terrain = true; ThingTerrainCommands.UiReply += TerrainReply;
        }
        static void TerrainReply(System.Collections.Generic.IReadOnlyList<string> reply)
        {
            if (!terrain || !ThingPanel.CanAdmin || ThingPanel.Window == null) return;
            if (reply.Count == 7 && reply[0] == "THING_TERRAIN_PREVIEW")
            {
                terrainPreview = true; ThingPanel.Message(reply[6]);
                ThingPanelForms.Confirm(ThingPanel.Window, reply[6], () => { terrainPreview = false; ThingTerrainCommands.Run(new[] { "terrain", "confirm" }, fromUi: true); },
                    () => { terrainPreview = false; ThingTerrainCommands.Run(new[] { "terrain", "cancel" }, fromUi: true); });
            }
            else ThingPanel.Message(string.Join("\n", reply));
        }
        internal static void Drops()
        {
            if (!ThingPanel.CanAdmin || ThingPanel.Window == null) return;
            if (!drops) { drops = true; ThingDropCommands.UiReply += DropReply; }
            ThingDropCommands.Run(new[] { "drops" }, fromUi: true);
        }
        static void DropReply(System.Collections.Generic.IReadOnlyList<string> reply)
        {
            if (!drops || !ThingPanel.CanAdmin || ThingPanel.Window == null) return;
            if (reply.Count == 3 && reply[0] == "THING_DROPS_CONFIRM")
            {
                dropsPreview = true; ThingPanel.Message(reply[2]);
                ThingPanelForms.Confirm(ThingPanel.Window, reply[2], () => { dropsPreview = false; ThingDropCommands.Run(new[] { "drops", "confirm" }, fromUi: true); },
                    () => { dropsPreview = false; ThingDropCommands.Run(new[] { "drops", "cancel" }, fromUi: true); });
            }
            else ThingPanel.Message(string.Join("\n", reply));
        }
        internal static void Reset()
        {
            if (terrain) { ThingTerrainCommands.UiReply -= TerrainReply; if (terrainPreview) ThingTerrainCommands.Run(new[] { "terrain", "cancel" }); ThingTerrainCommands.ReleaseUiPending(); }
            if (drops) { ThingDropCommands.UiReply -= DropReply; if (dropsPreview) ThingDropCommands.Run(new[] { "drops", "cancel" }); ThingDropCommands.ReleaseUiPending(); }
            terrain = drops = terrainPreview = dropsPreview = false;
        }
    }
}
