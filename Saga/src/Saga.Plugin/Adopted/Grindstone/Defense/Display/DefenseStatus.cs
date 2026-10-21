// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Display/DefenseStatus.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A status effect that only shows something: an icon in the HUD's status column while <see cref="Showing"/> says
    /// so, with <see cref="Label"/> as its text (a time, a stack count). SEMan updates it every frame on the local
    /// player and removes it once IsDone is true; the state it shows lives in the Defense feature it belongs to, so
    /// the icon can never outlast it. Status effects stay on the player's own client (only their attribute bits reach
    /// the ZDO, and these have none), so nothing is sent. Clone copies the delegates with the rest.
    /// </summary>
    public class DefenseStatus : StatusEffect
    {
        public Func<bool> Showing;
        public Func<string> Label;

        public override bool IsDone() => Showing == null || !HookGuard.Run("defense status", Showing, false);

        public override string GetIconText() => Label != null ? HookGuard.Run("defense status text", Label, "") : "";
    }
}
