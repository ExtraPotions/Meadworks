// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Hex.Runtime;

internal static partial class HexWindow
{
    static void Help(GameObject control, string title, string purpose, string reason = "")
    {
        var graphic = control.GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = true;
        KitTooltip.Set(control, title, purpose + (reason.Length > 0 && reason != purpose ? "\n\n" + reason : ""));
    }
    static void NumberHelp(KitAlmanacNumber number, string title, string purpose, string reason = "")
    {
        Help(number.Field.Input.gameObject, title, purpose, reason);
        Help(number.Less.gameObject, title, purpose, reason);
        Help(number.More.gameObject, title, purpose, reason);
        if (number.Slider != null) Help(number.Slider.gameObject, title, purpose, reason);
    }
    static string ActionHelp(string id) => HexText.Text(id switch
    {
        "heal" => "help_heal", "rested" => "help_rested", "power" => "help_power",
        "reset_speeds" => "help_reset_speeds", "poi" => "help_poi", "estimatepoi" => "help_estimatepoi", "clearpoi" => "help_clearpoi",
        "reveal" => "help_reveal", "reset" => "map_confirm_reset", "seed" => "help_seed",
        "debug_kill" => ZNet.instance != null && ZNet.instance.IsServer() ? "debug_confirm_kill_body" : "debug_confirm_remote_kill_body",
        "debug_drops" => ZNet.instance != null && ZNet.instance.IsServer() ? "debug_confirm_drops_body" : "debug_confirm_remote_drops_body",
        "debug_tame" => ZNet.instance != null && ZNet.instance.IsServer() ? "debug_confirm_tame_body" : "debug_confirm_remote_tame_body",
        "debug_event" => "debug_confirm_event_body",
        "debug_fly" => "fly", "debug_build" => "debug_mode",
        "savepreset" => "help_savepreset", "reloadpresets" => "help_reloadpreset",
        _ => "quick_actions"
    });
}
