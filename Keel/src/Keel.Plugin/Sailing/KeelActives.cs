// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Wind Call and Lookout keys and gamepad chords (design 4.4, C07) and their panel meters (user
// decision 2026-10-08: a Crucible.UI KitMeter per ability).
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Display;
using ExtraPotions.Keel.Sailing;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    internal static class KeelActives
    {
        /// <summary>Every frame on a graphical client: the local player's keys and chords while the game takes their input.</summary>
        public static void Tick()
        {
            var player = Player.m_localPlayer;
            var settings = Plugin.Settings;
            if (player == null || settings == null || !SailingPerks.Active || !player.TakeInput()) return;
            if (Pressed(settings, KeelSettings.WindCall.Key, KeelSettings.WindCall.Pad)) Guard("wind call key", () => KeelWindCall.Pressed(player));
            if (Pressed(settings, KeelSettings.Lookout.Key, KeelSettings.Lookout.Pad)) Guard("lookout key", () => KeelLookout.Pressed(player));
        }

        /// <summary>The panel meter of Wind Call or Lookout for the local player.</summary>
        public static AbilityMeter Meter(bool windCall)
        {
            var settings = Plugin.Settings;
            if (settings == null || !SailingPerks.Active) return PanelModel.Ability(false, 0f, 0f, 0f, 0f);
            var unlockable = windCall ? KeelWindCall.Unlockable : KeelLookout.Unlockable;
            var level = windCall ? KeelWindCall.Level : KeelLookout.Level;
            var cooldown = windCall ? KeelWindCall.Cooldown : KeelLookout.Cooldown;
            var length = settings.Get(windCall ? KeelSettings.WindCall.Cooldown : KeelSettings.Lookout.Cooldown);
            return PanelModel.Ability(unlockable, KeelSailing.Skill!.Local(), level, cooldown.Left(Time.time), length);
        }

        private static bool Pressed(KeelConfig settings, KeelSetting<string> key, KeelSetting<string> pad)
        {
            if (ShortcutPressed(settings, key)) return true;
            return PadPressed(settings.Get(pad));
        }

        /// <summary>
        /// The key as the player's own entry while no server lock replaces it: Crucible keeps the parsed chord of an entry,
        /// whereas a bare shortcut is serialized and parsed again on every call (every frame, for both keys).
        /// </summary>
        private static bool ShortcutPressed(KeelConfig settings, KeelSetting<string> key)
        {
            return settings.ShortcutLocked(key) ? MeadworksInput.Pressed(settings.Shortcut(key)) : MeadworksInput.Pressed(settings.ShortcutEntry(key));
        }

        private static bool PadPressed(string chord)
        {
            var names = PadChord.Cached(chord);
            if (names.Count == 0 || !MeadworksInput.Allows(InputContext.Gameplay)) return false;
            var allHeld = true;
            var anyDown = false;
            for (var i = 0; i < names.Count; i++)
            {
                if (!GamepadMap.TryParse(names[i], out var button)) return false;
                var input = GamepadMap.ZInputName(button);
                if (!ZInput.GetButton(input)) allHeld = false;
                if (ZInput.GetButtonDown(input)) anyDown = true;
            }
            return allHeld && anyDown;
        }

        private static void Guard(string what, Action action)
        {
            try { action(); }
            catch (Exception error) { KeelShips.Warn(what, error); }
        }
    }
}
