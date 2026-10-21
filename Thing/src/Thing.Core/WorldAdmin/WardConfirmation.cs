// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: server confirmation, bound to the requesting session and native character, that lapses unless refreshed.
namespace ExtraPotions.Thing.WorldAdmin
{
    public sealed class WardConfirmation
    {
        /// <summary>Refresh interval plus a margin for the reply, so a confirmed override does not lapse between refreshes.</summary>
        public const double MaxAgeSeconds = WardStatusSchedule.RefreshSeconds + 15;
        long generation = -1;
        string character = "";
        double started, expires;

        public void Confirm(long session, string nativeCharacter, double requestStarted)
        {
            if (session < 0 || string.IsNullOrEmpty(nativeCharacter) || double.IsNaN(requestStarted) || double.IsInfinity(requestStarted) || requestStarted < 0)
            { Clear(); return; }
            generation = session;
            character = nativeCharacter;
            started = requestStarted;
            expires = requestStarted + MaxAgeSeconds;
        }

        public bool Allows(long session, string nativeCharacter, double now) => character.Length > 0 && generation == session
            && character == nativeCharacter && !double.IsNaN(now) && !double.IsInfinity(now) && now >= started && now < expires;

        public void Clear() { generation = -1; character = ""; expires = 0; }
    }
}
