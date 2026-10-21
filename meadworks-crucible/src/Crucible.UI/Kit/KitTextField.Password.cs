// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using GUIFramework;
using HarmonyLib;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public sealed partial class KitTextField
    {
        static readonly ConditionalWeakTable<GuiInputField,KitTextField> passwords = new ConditionalWeakTable<GuiInputField,KitTextField>();
        static void TrackPassword(KitTextField field) => passwords.Add(field.Input,field);
        bool passwordDestroyed, passwordKeyboardOpen, rejectPasswordKeyboard;
        bool PasswordClosed => Mode == KitFieldMode.Password
            && (passwordDestroyed || Input == null || !Input.gameObject.activeInHierarchy);

        /// <summary>Clears the password without invoking product events. No mutable password buffer is persisted by the kit.</summary>
        public void ClearPassword()
        {
            if(Mode != KitFieldMode.Password) return;
            before = "";
            debounce.Cancel();
            if(Input != null) Input.SetTextWithoutNotify("");
        }

        internal void ClosePassword(bool destroyed = true)
        {
            passwordDestroyed |= destroyed;
            if(passwordKeyboardOpen) rejectPasswordKeyboard = true;
            ClearPassword();
        }

        void PasswordKeyboardState(VirtualKeyboardState state)
        {
            if(state == VirtualKeyboardState.Open) passwordKeyboardOpen = true;
            else if(state == VirtualKeyboardState.Close) {
                passwordKeyboardOpen = false;
                rejectPasswordKeyboard = false;
            }
        }

        internal static bool AcceptPasswordKeyboard(GuiInputField input)
        {
            // A native callback can retain the managed input even after Unity destroyed its object.
            // Ephemeron ownership lets that callback find its closed field without retaining dead UI.
            return !passwords.TryGetValue(input,out var field) || !field.PasswordClosed && !field.rejectPasswordKeyboard;
        }
    }

    internal sealed class KitPasswordLifetime : MonoBehaviour
    {
        internal KitTextField Field;
        void OnDisable() => Field?.ClosePassword(false);
        void OnDestroy() => Field?.ClosePassword();
    }

    [HarmonyPatch(typeof(GuiInputField),"ValidateVirtualKeyboardText")]
    internal static class KitPasswordKeyboardPatch
    {
        // Reject a late result before the native field writes or dispatches it. Ordinary fields are untouched.
        static bool Prefix(GuiInputField __instance) => KitTextField.AcceptPasswordKeyboard(__instance);
    }
}
