using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.Hotkeys
{
    /// <summary>
    /// A shortcut read the same way in every mod. With modifiers it is its main key going down this frame with all its
    /// modifiers held and no other Shift, Ctrl or Alt; other keys may be held, so it fires while the player walks with W
    /// (BepInEx's own <c>IsDown</c> refuses a shortcut while any other key at all is held). A single key is the key going
    /// down with no Shift, Ctrl or Alt held, so it never fires together with a modified shortcut on the same key (Z next to
    /// Ctrl + Z). Nothing counts while the player types (<see cref="Typing.Active"/>). No key (None) never fires.
    /// Whether a key is held is read as the game reads it (<see cref="KeyHeld"/>).
    /// </summary>
    public static class Hotkey
    {
        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        /// <summary>
        /// Each setting's modifiers as an array, read once: BepInEx hands them out as a LINQ sequence, which makes garbage
        /// on every read, and the keys are read every frame. Dropped when the setting changes.
        /// </summary>
        private static readonly Dictionary<ConfigEntry<KeyboardShortcut>, KeyCode[]> Modifiers =
            new Dictionary<ConfigEntry<KeyboardShortcut>, KeyCode[]>();

        private static readonly HashSet<ConfigEntry<KeyboardShortcut>> Watched = new HashSet<ConfigEntry<KeyboardShortcut>>();

        /// <summary>
        /// Down this frame, modifiers honoured, nothing being typed. Mods read their keys many times a frame, so the main
        /// key going down is tested first: the modifiers and the typing test only run on the frame it does.
        /// </summary>
        public static bool Pressed(ConfigEntry<KeyboardShortcut>? key)
        {
            if (key == null)
            {
                return false;
            }
            KeyCode main = key.Value.MainKey;
            if (main == KeyCode.None || !Input.GetKeyDown(main))
            {
                return false;
            }
            KeyCode[] modifiers = ModifiersOf(key);
            bool chord = modifiers.Length > 0
                ? AllHeld(modifiers) && !OtherModifierHeld(main, modifiers)
                : !ModifierHeld(main);
            return chord && !Typing.Active;
        }

        /// <summary>The main key held with its modifiers (for modifier settings such as LeftShift), nothing being typed.</summary>
        public static bool Held(ConfigEntry<KeyboardShortcut>? key)
        {
            if (key == null)
            {
                return false;
            }
            KeyCode main = key.Value.MainKey;
            return main != KeyCode.None && KeyHeld(main) && AllHeld(ModifiersOf(key)) && !Typing.Active;
        }

        /// <summary>
        /// A key held now. Shift, Ctrl and Alt count only when both Unity's old <c>Input</c> and the game's own input
        /// (<c>ZInput</c>, Unity's input system) say so: each can keep a modifier held that was let go in another window
        /// (Alt + Tab out, Alt + Z to an overlay, back with the mouse). The old input stuck on 2026-10-05, the game's on
        /// 2026-10-06; either way a stuck Left Alt made Mantle's plain 1 to 3 drink meads and the hotbar keys do nothing.
        /// A modifier really held reads held in both.
        /// </summary>
        public static bool KeyHeld(KeyCode key) =>
            Array.IndexOf(ModifierKeys, key) >= 0 ? ZInput.GetKey(key, false) && Input.GetKey(key) : Input.GetKey(key);

        /// <summary>
        /// A set shortcut with modifiers, all of them held, its main key or not (Alt while the player reaches for Alt + 1),
        /// nothing being typed. False for a shortcut without modifiers.
        /// </summary>
        public static bool ModifiersHeld(ConfigEntry<KeyboardShortcut>? key)
        {
            if (key == null || key.Value.MainKey == KeyCode.None)
            {
                return false;
            }
            KeyCode[] modifiers = ModifiersOf(key);
            return modifiers.Length > 0 && AllHeld(modifiers) && !Typing.Active;
        }

        private static KeyCode[] ModifiersOf(ConfigEntry<KeyboardShortcut> key)
        {
            if (Modifiers.TryGetValue(key, out KeyCode[] modifiers))
            {
                return modifiers;
            }
            if (Watched.Add(key))
            {
                key.SettingChanged += (sender, args) => Modifiers.Remove(key);
            }
            modifiers = key.Value.Modifiers.ToArray();
            Modifiers[key] = modifiers;
            return modifiers;
        }

        private static bool AllHeld(KeyCode[] keys)
        {
            foreach (KeyCode held in keys)
            {
                if (!KeyHeld(held))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A Shift, Ctrl or Alt key that is not part of the shortcut is held.</summary>
        private static bool OtherModifierHeld(KeyCode main, KeyCode[] modifiers)
        {
            foreach (KeyCode modifier in ModifierKeys)
            {
                if (modifier != main && Array.IndexOf(modifiers, modifier) < 0 && KeyHeld(modifier))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Any Shift, Ctrl or Alt other than the key itself is held.</summary>
        private static bool ModifierHeld(KeyCode self)
        {
            foreach (KeyCode modifier in ModifierKeys)
            {
                if (modifier != self && KeyHeld(modifier))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
