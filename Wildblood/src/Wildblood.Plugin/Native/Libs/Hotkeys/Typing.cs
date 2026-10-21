// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, ValheimModLibs, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.AdoptedLibs.Hotkeys
{
    /// <summary>
    /// Whether the player is typing, so no hotkey fires: the chat has focus, the console or the game's text input (signs,
    /// tame names) is open, a Unity input field is selected, or a window the mod registered (<see cref="AddWindow"/>, such as
    /// its YAML editor) is open. Every mod merges its own copy, so the registered windows are the mod's own.
    /// </summary>
    public static class Typing
    {
        private static readonly List<Func<bool>> Windows = new List<Func<bool>>();

        public static bool Active => ChatFocused || Console.IsVisible() || TextInput.IsVisible() || InputFieldSelected || WindowOpen;

        /// <summary>A text window of the mod's own (drawn with IMGUI, so no input field shows it) that silences its hotkeys while open.</summary>
        public static void AddWindow(Func<bool> open)
        {
            if (open != null)
            {
                Windows.Add(open);
            }
        }

        private static bool ChatFocused => Chat.instance != null && Chat.instance.HasFocus();

        private static bool InputFieldSelected
        {
            get
            {
                EventSystem system = EventSystem.current;
                GameObject? selected = system != null ? system.currentSelectedGameObject : null;
                if (selected == null)
                {
                    return false;
                }
                return selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<InputField>() != null;
            }
        }

        private static bool WindowOpen
        {
            get
            {
                foreach (Func<bool> open in Windows)
                {
                    if (open())
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
