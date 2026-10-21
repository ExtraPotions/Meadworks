// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// The reference run speed the movement clamp holds every creature under, so none can outrun a fleeing player. It
    /// tracks the local player's own base run speed and remembers the last value seen, so a headless server (which has
    /// no local player) still has a sane figure to clamp against once any player has been observed. Before that, and on
    /// a pure dedicated host, a documented fallback stands in - see DECISIONS.md on why it is what it is.
    /// </summary>
    internal static class PlayerSpeed
    {
        /// <summary>Valheim's player base run speed; the stand-in until a real local player is seen.</summary>
        public const float Fallback = 8f;

        private static float _reference = Fallback;

        public static float Reference()
        {
            Player local = Player.m_localPlayer;
            if (local != null && local.m_runSpeed > 0f)
            {
                _reference = local.m_runSpeed;
            }
            return _reference;
        }
    }
}
