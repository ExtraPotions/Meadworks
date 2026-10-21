// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, ValheimModLibs, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ExtraPotions.Wildblood.AdoptedLibs.Hotkeys
{
    /// <summary>
    /// A shortcut's name short enough for a key cap on the screen: modifiers first, their side dropped, the number keys
    /// bare, joined with "+": "Z", "Shift+Z", "Ctrl+1". Empty for no key (None).
    /// </summary>
    public static class KeyNames
    {
        public static string Short(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
            {
                return "";
            }
            List<string> parts = new List<string>();
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                parts.Add(Short(modifier));
            }
            parts.Add(Short(shortcut.MainKey));
            return string.Join("+", parts);
        }

        /// <summary>One key: LeftShift and RightShift are Shift, the Controls Ctrl, Alpha1 is 1; the rest as Unity names it.</summary>
        public static string Short(KeyCode key)
        {
            string name = key.ToString();
            if (name.StartsWith("Alpha") && name.Length == 6)
            {
                return name.Substring(5);
            }
            string side = name.StartsWith("Left") ? name.Substring(4) : name.StartsWith("Right") ? name.Substring(5) : "";
            switch (side)
            {
                case "Shift":
                case "Alt":
                case "Command":
                    return side;
                case "Control":
                    return "Ctrl";
                default:
                    return name;
            }
        }
    }
}
