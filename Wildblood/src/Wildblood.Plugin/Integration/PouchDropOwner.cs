// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the owner-local durable ledger behind Thieving pouch drops (B06c). One ledger per world under
// BepInExRootPath/Wildblood.transactions/drops; opened lazily on the machine that owns a dying or despawning thief.
using System;
using System.Globalization;
using System.IO;
using BepInEx;
using ExtraPotions.Wildblood.Transfers;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static class PouchDropOwner
{
    static PouchDropLedger? ledger;
    static long world;
    static bool faulted;
    static double nextWarning;

    /// <summary>The current world's ledger, or null when it cannot be opened (the caller then creates no receipted drop).</summary>
    internal static PouchDropLedger? Ledger()
    {
        long current = ZNet.instance != null && ZNet.m_world != null ? ZNet.instance.GetWorldUID() : 0;
        if (current == 0 || current != CruciblePlugin.Session.WorldId) return null;
        if (ledger != null && world == current) return ledger;
        Reset();
        world = current;
        try
        {
            string identity = TheftMetadata.Hash(System.Text.Encoding.UTF8.GetBytes("drops\n" + current.ToString(CultureInfo.InvariantCulture)));
            ledger = new PouchDropLedger(Path.Combine(Paths.BepInExRootPath, "Wildblood.transactions", "drops", identity));
            faulted = false;
            if (ledger.Unresolved.Count != 0)
                Warn("Wildblood pouch drops: " + ledger.Unresolved.Count + " UNKNOWN drop decision(s) retained; nothing is recreated.");
            return ledger;
        }
        catch (Exception error)
        {
            faulted = true;
            Warn("Wildblood pouch drop ledger unavailable: " + error.GetType().Name);
            return null;
        }
    }

    internal static bool Faulted => faulted;

    internal static void Warn(string message)
    {
        double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
        if (now < nextWarning) return;
        nextWarning = now + 10;
        Plugin.NativeLogger?.LogWarning(message);
    }

    internal static void Reset()
    {
        ledger?.Dispose();
        ledger = null;
        world = 0;
    }
}
