// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
//
// Follow Command (design D6) reimplemented from the observed behavior of Cartur's Follow Command
// (https://github.com/jekkle/cartur-follow-command): no license file was found in that repository on 2026-10-07, so no
// code was copied; only the documented behavior (the game's own Tameable commandable flag set on other tameables).
namespace ExtraPotions.Shepherd.Husbandry
{
    /// <summary>
    /// Follow Command decisions: Shepherd sets the game's <c>Tameable.m_commandable</c> flag on tameables the game left
    /// uncommandable, so Use toggles the native follow/stay command (RPC, patrol and the saved follow target stay the
    /// game's). Summons and tameables without native follow AI are left alone; only flags Shepherd flipped are restored when Shepherd stops; petting
    /// contentment keys on the game's own flag, so a boar that now follows can still be petted content.
    /// </summary>
    public static class FollowRules
    {
        public static bool Enables(bool active, bool nativelyCommandable, bool summon, bool supportsFollow) =>
            active && !nativelyCommandable && !summon && supportsFollow;

        public static bool Restores(bool flippedByShepherd, bool active) => flippedByShepherd && !active;

        public static bool PetsForContent(bool nativelyCommandable) => !nativelyCommandable;
    }
}
