// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.Hotkeys;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;
using ExtraPotions.Wildblood.AdoptedLibs.WindowInput;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>
    /// The death recap's place in the frame loop, on the plugin's own object: finishes pending recaps and opens or closes
    /// the window on its key. The recording itself runs beside it (<see cref="FrameGrabber"/>).
    /// </summary>
    internal sealed class RecapHost : MonoBehaviour
    {
        // One delegate for the whole session: a method group passed each frame would allocate a new one every frame.
        private static readonly Action TickOnce = Tick;

        /// <summary>From the plugin's Awake: the window joins the game's windows, and recording starts on the host object.</summary>
        public static void Attach(GameObject host, Harmony harmony)
        {
            GameWindow.Install(harmony);
            GameWindow.Add(() => Window.RecapWindow.IsOpen, Window.RecapWindow.Close);
            host.AddComponent<RecapHost>();
            host.AddComponent<FrameGrabber>();
        }

        private void Update() => Guard.Run("death recap", TickOnce);

        private static void Tick()
        {
            RecapStore.Update();
            if (Hotkey.Pressed(RecapSettings.Key.Raw) && InventoryGui.instance != null && !Console.IsVisible() && !Menu.IsVisible())
            {
                Window.RecapWindow.Toggle();
            }
        }
    }
}
