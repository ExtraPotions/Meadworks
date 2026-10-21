#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraToggle.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Timber.Runtime;
using UnityEngine;

namespace Timber.Camera
{
    /// <summary>
    /// Brings the camera out and back, after the local player's update. Out: Toggle Key or Gamepad Toggle while the
    /// game takes input (no inventory, chat, console, map, menu or build menu), with a build tool in hand, standing in
    /// a station's camera area, with Entry Needs Resting and Entry Min Comfort met; it starts where the game camera is.
    /// Back: the key again, or at once when the tool leaves the hand (Hide, R, or unequipping ends the game's place
    /// mode), the player dies or teleports, takes a seat with its own camera (a ship's helm), the game's free fly
    /// camera starts, the setting is turned off, or no station's area holds the player any more.
    /// </summary>
    public static class CameraToggle
    {
        /// <summary>After the local player's update (Timber camera update hook).</summary>
        public static void Tick(Player __instance)
        {
            if (__instance != Player.m_localPlayer)
                return;
            if (!CameraModule.Allowed) { CameraState.Exit(); return; }
            bool pressed = TogglePressed(__instance);
            if (CameraState.Active)
            {
                if (pressed || MustEnd(__instance))
                    CameraState.Exit();
                return;
            }
            if (pressed && CameraSettings.Enabled.Value && CanUse(__instance))
                TryEnter(__instance);
        }

        private static bool TogglePressed(Player player)
        {
            if (!MeadworksInput.Allows(InputContext.Gameplay) || !player.TakeInput() || Hud.InRadial() || Hud.IsPieceSelectionVisible())
                return false;
            return MeadworksInput.Pressed(CameraPrefs.ToggleKey.Value) || PadToggle.Pressed();
        }

        /// <summary>The camera may be out for this player now: build mode, alive, not teleporting, not seated at a helm, no free fly camera.</summary>
        public static bool CanUse(Player player)
        {
            return player.InPlaceMode() && !player.IsDead() && !player.IsTeleporting() && player.GetAttachCameraPoint() == null
                && GameCamera.instance != null && !GameCamera.InFreeFly();
        }

        private static bool MustEnd(Player player)
        {
            return !CameraSettings.Enabled.Value || InventoryGui.IsVisible() || !CanUse(player)
                || !CameraArea.Contains(player.transform.position) || !CameraArea.Contains(CameraState.Position);
        }

        private static void TryEnter(Player player)
        {
            if (!CameraArea.Contains(player.transform.position))
            {
                CameraModule.Center(TimberText.Text(CameraModule.NoStation));
                return;
            }
            string missing = CameraNeeds.Describe(player, CameraSettings.EntryNeedsResting, CameraSettings.EntryMinComfort);
            if (missing != null)
            {
                CameraModule.Center(TimberText.Text(CameraModule.EntryNeeds) + ": " + missing);
                return;
            }
            Transform view = GameCamera.instance.transform;
            CameraState.Enter(player, view);
            CameraState.Position = CameraArea.Clamp(view.position);
        }
    }
}
