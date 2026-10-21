// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Nightfall's storm as the local player's body feels it. While the storm is at least half drawn on this client
    /// (<see cref="NightfallBlend.Storming"/>), the game's own weather check for this player's status effects reads the
    /// storm's flags as at midnight on top of the real weather's - the thunderstorm is wet, and cold at night - so the
    /// player turns Wet and Cold just as under a real storm at night, and everything that protects from those still
    /// does: a roof, a fire, shelter, frost resistance, a warm and cosy spot. A place that is already freezing stays
    /// freezing. The game's weather flags are lent for that one call only and put back straight after
    /// (<see cref="Patches.NightfallChillPatch"/>), so nothing else on this client - rain wear on the pieces it owns,
    /// fires and cinders, spawns - ever feels a storm the other players cannot see.
    /// </summary>
    internal static class NightfallChill
    {
        private static bool _lent;
        private static bool _cold;
        private static bool _wet;
        private static bool _freezing;

        /// <summary>Lends the storm's flags to the game's weather for the status-effect check about to run.</summary>
        public static bool Begin()
        {
            EnvSetup? storm = NightfallBlend.Storm;
            if (_lent || storm == null)
            {
                return false;
            }
            _cold = EnvMan.s_isCold;
            _wet = EnvMan.s_isWet;
            _freezing = EnvMan.s_isFreezing;
            _lent = true;
            EnvMan.s_isCold = _cold || storm.m_isCold || storm.m_isColdAtNight;
            EnvMan.s_isWet = _wet || storm.m_isWet;
            EnvMan.s_isFreezing = _freezing || storm.m_isFreezing || storm.m_isFreezingAtNight;
            return true;
        }

        /// <summary>Puts the game's own weather flags back; nothing when none were lent.</summary>
        public static void End()
        {
            if (!_lent)
            {
                return;
            }
            _lent = false;
            EnvMan.s_isCold = _cold;
            EnvMan.s_isWet = _wet;
            EnvMan.s_isFreezing = _freezing;
        }
    }
}
