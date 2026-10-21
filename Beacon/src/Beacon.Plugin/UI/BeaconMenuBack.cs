// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (BC07b): keyboard Escape for Beacon's main-menu layers; pad B stays with the kit windows.
using BepInEx.Configuration;
using ExtraPotions.Beacon.Joining;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconMenuBack
    {
        static readonly KeyboardShortcut EscapeKey = new KeyboardShortcut(KeyCode.Escape);
        static bool typedLastFrame;

        public static void Tick()
        {
            if (!Plugin.Ready || Game.instance != null || FejdStartup.instance == null || !MeadworksHeadless.AllowsInput) { typedLastFrame = false; return; }
            var typing = MeadworksInput.Typing || KitTextField.Typing;
            var panelOpen = BeaconPanel.Window?.IsOpen == true;
            var dialog = panelOpen ? KitAlmanacDialog.Active : null;
            var state = new MenuBackState
            {
                Escape = !typing && MeadworksInput.Pressed(EscapeKey, InputContext.Global),
                Typing = typing,
                TypedLastFrame = typedLastFrame,
                DialogOpen = dialog != null,
                DialogTyping = dialog?.TypingField?.Invoke() == true,
                SaveOfferOpen = BeaconSaveOffer.Showing,
                PanelOpen = panelOpen,
                WaitPending = BeaconAutoJoin.Waiting,
            };
            typedLastFrame = typing || state.DialogTyping;
            switch (MenuBack.Decide(state))
            {
                case MenuBackAction.CloseDialog: dialog!.Dispose(); break;
                case MenuBackAction.DeclineSaveOffer: BeaconSaveOffer.Reset(); break;
                case MenuBackAction.ClosePanel: BeaconPanel.Reset(); break;
                case MenuBackAction.CancelWait: BeaconAutoJoin.Cancel(Plugin.Log); break;
            }
        }
    }
}
