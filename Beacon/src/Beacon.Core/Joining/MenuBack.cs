// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (BC07b gamepad and keyboard pass): one main-menu Escape closes one Beacon layer.
namespace ExtraPotions.Beacon.Joining
{
    /// <summary>What one keyboard Escape on the main menu does to Beacon's layers.</summary>
    public enum MenuBackAction
    {
        None,
        CloseDialog,
        DeclineSaveOffer,
        ClosePanel,
        CancelWait,
    }

    /// <summary>The main-menu facts Escape depends on, read once per frame.</summary>
    public struct MenuBackState
    {
        public bool Escape;
        public bool Typing;
        public bool TypedLastFrame;
        public bool DialogOpen;
        public bool DialogTyping;
        public bool SaveOfferOpen;
        public bool PanelOpen;
        public bool WaitPending;
    }

    /// <summary>
    /// The game routes Escape to mod windows only in a world (its pause menu); on the main menu Beacon does it itself.
    /// A focused field keeps its own Escape, including the frame it let go, and only the topmost layer closes.
    /// </summary>
    public static class MenuBack
    {
        public static MenuBackAction Decide(MenuBackState state)
        {
            if (!state.Escape || state.Typing || state.TypedLastFrame) return MenuBackAction.None;
            if (state.DialogOpen) return state.DialogTyping ? MenuBackAction.None : MenuBackAction.CloseDialog;
            if (state.SaveOfferOpen) return MenuBackAction.DeclineSaveOffer;
            if (state.PanelOpen) return MenuBackAction.ClosePanel;
            return state.WaitPending ? MenuBackAction.CancelWait : MenuBackAction.None;
        }
    }
}
