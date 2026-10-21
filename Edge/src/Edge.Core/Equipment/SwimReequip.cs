// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, clean room (design C1 to C3, C5). Written from public behavior only: the KeepWeaponsAfterSwim
// 1.0.1 README, CHANGELOG and config (modes AutoRestore, KeepInHand, Off; RestoreDelay 0.5 s against shoreline flicker;
// weapons the player put away stay away). KeepWeaponsAfterSwim has no license and no public source; its DLL was not
// opened. The game members it hooks (Humanoid.HideHandItems, ShowHandItems, Character.IsSwimming) are the game's own.
using System;
using ExtraPotions.Edge.Config;

namespace ExtraPotions.Edge.Equipment
{
    public enum SwimAction { None, Restore }

    /// <summary>
    /// The local player's swim re-equip state. The plugin asks <see cref="AllowHide"/> from the game's hide call, calls
    /// <see cref="Update"/> every frame, and shows the hidden items itself on <see cref="SwimAction.Restore"/> (setting
    /// <see cref="Restoring"/> around that call, so the last-shield rule does not add a second item, C5). Main thread only.
    /// </summary>
    public sealed class SwimReequip
    {
        private double? dryAt;

        public SwimReequip(SwimMode mode, float delay) => Configure(mode, delay);

        public SwimMode Mode { get; private set; }

        public float Delay { get; private set; }

        /// <summary>The items in the hidden slots were put away by swimming, not by the player.</summary>
        public bool HiddenBySwim { get; private set; }

        /// <summary>Set by the plugin while it shows the items this state asked to restore.</summary>
        public bool Restoring { get; set; }

        /// <summary>Applies the current settings; switching to Off drops a pending restore.</summary>
        public void Configure(SwimMode mode, float delay)
        {
            Mode = EdgeSettings.Equipment.SwimMode.Normalize(mode);
            Delay = EdgeSettings.Equipment.RestoreDelay.Normalize(delay);
            if (Mode != SwimMode.AutoRestore) Forget();
        }

        /// <summary>
        /// The game is about to hide the hand items. Returns false to keep them in hand (KeepInHand while swimming). A hide
        /// while swimming with items in hand is remembered for AutoRestore; a hide on land is the player's own choice.
        /// </summary>
        public bool AllowHide(bool swimming, bool handsHaveItems)
        {
            switch (Mode)
            {
                case SwimMode.KeepInHand:
                    return !swimming;
                case SwimMode.AutoRestore:
                    if (!swimming) Forget();
                    else if (handsHaveItems)
                    {
                        HiddenBySwim = true;
                        dryAt = null;
                    }
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>The player showed or put away items by hand: nothing left to restore.</summary>
        public void PlayerShowed() => Forget();

        /// <summary>Death, teleport, logout or a new character.</summary>
        public void Reset()
        {
            Forget();
            Restoring = false;
        }

        /// <summary>Restore once the player has been out of the water for the delay; a new swim restarts the wait.</summary>
        public SwimAction Update(double now, bool swimming, bool handsHidden)
        {
            if (!HiddenBySwim || Mode != SwimMode.AutoRestore) return SwimAction.None;
            if (!handsHidden)
            {
                Forget();
                return SwimAction.None;
            }
            if (swimming)
            {
                dryAt = null;
                return SwimAction.None;
            }
            if (!dryAt.HasValue) dryAt = now;
            if (now - dryAt.Value + 1e-9 < Delay) return SwimAction.None;
            Forget();
            return SwimAction.Restore;
        }

        private void Forget()
        {
            HiddenBySwim = false;
            dryAt = null;
        }
    }
}
