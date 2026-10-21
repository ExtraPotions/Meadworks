// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (BC04b interim card, 2026-10-08 decision 2): the lines of the auto-join and reconnect card.
using System;
using System.Collections.Generic;
using ExtraPotions.Beacon.Localization;

namespace ExtraPotions.Beacon.Joining
{
    /// <summary>One localized line: a key of <see cref="BeaconTextKeys"/> and its arguments.</summary>
    public readonly struct CardLine
    {
        public CardLine(string key, params object[] arguments)
        {
            Key = key;
            Arguments = Array.AsReadOnly(arguments ?? Array.Empty<object>());
        }

        public string Key { get; }
        public IReadOnlyList<object> Arguments { get; }
    }

    /// <summary>What the interim kit card says while an auto-join counts down or a reconnect waits; it never holds a password.</summary>
    public static class JoinCard
    {
        public static IReadOnlyList<CardLine> Countdown(string serverName, int seconds) => new[]
        {
            new CardLine(BeaconTextKeys.CardAutoJoin, serverName ?? "", Math.Max(0, seconds)),
            new CardLine(BeaconTextKeys.CardCancelHint),
        };

        public static IReadOnlyList<CardLine> Reconnect(int attempt, int limit, int seconds) => new[]
        {
            new CardLine(BeaconTextKeys.CardReconnect, attempt, limit, Math.Max(0, seconds)),
            new CardLine(BeaconTextKeys.CardCancelHint),
        };
    }
}
