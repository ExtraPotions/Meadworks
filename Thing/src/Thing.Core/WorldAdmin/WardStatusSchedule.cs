// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: when the client asks the server whether its ward override still stands.
namespace ExtraPotions.Thing.WorldAdmin
{
    /// <summary>
    /// Event-driven ward status requests with one slow safety refresh. Every request is a Crucible Operation and the
    /// authority keeps its request id for the session, so the override must never poll per frame.
    /// </summary>
    public sealed class WardStatusSchedule
    {
        /// <summary>
        /// Slow re-confirmation while the override is on. Setting changes, grants, session and character changes already
        /// act at once (the synced server setting is checked locally, events ask immediately, a new session or character
        /// clears the override). The refresh only bounds how long a server-side revocation the client cannot observe
        /// (for example the admin entry removed from the server's admin list) keeps the local bypass: at most this long.
        /// One request per minute per admin keeps the authority's request ledger use negligible.
        /// </summary>
        public const double RefreshSeconds = 60;

        double next = double.PositiveInfinity;
        bool requested;

        /// <summary>A confirmed "on" reply for a request sent at <paramref name="requestStarted"/>.</summary>
        public void Confirmed(double requestStarted) =>
            next = double.IsNaN(requestStarted) || double.IsInfinity(requestStarted) ? double.PositiveInfinity : requestStarted + RefreshSeconds;

        /// <summary>A relevant change (setting push or reload): ask once, as soon as no request is in flight.</summary>
        public void Request() => requested = true;

        public bool Due(bool wanted, double now) => wanted && (requested || now >= next);

        /// <summary>A status request left; the next refresh is scheduled by its confirmation.</summary>
        public void Sent() { requested = false; next = double.PositiveInfinity; }

        public void Clear() { requested = false; next = double.PositiveInfinity; }
    }
}
