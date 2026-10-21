// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using Burrow.Core;
using Burrow.Reach;
using ExtraPotions.Meadworks.Crucible;

namespace Burrow.Stow
{
    internal static class InteractQuickStack
    {
        internal static bool TryRun(Container container, Humanoid character, bool hold)
        {
            var player = Player.m_localPlayer;
            if (hold || player == null || character != player || player.IsDead() || player.IsTeleporting()
                || player.InCutscene() || player.InAttack() || player.InDodge() || !StowSettings.Enabled.Value
                || Burrow.Integration.StowRowYield.SteppedAside || !MeadworksInput.Allows(InputContext.Gameplay)
                || UnifiedPopup.IsVisible() || !Keys.Held(ReachSettings.FillModifier))
                return false;
            // Native hover selection already applies eye distance and the target's hover offset.
            var hovered = player.GetHoverObject();
            if (container == null || hovered == null || hovered.GetComponentInParent<Container>() != container)
                return false;
            if (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, false))
                return false;
            if (Game.instance == null || !container.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID()))
                return false;
            return StowActions.QuickStackTo(container);
        }
    }
}
