// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Ghost admin setting for vanilla clients and the server refusal notice keys (docs/ghost-authority.md). Unity-free.
using System.Collections.Generic;
namespace ExtraPotions.Hex.Ghost
{
    /// <summary>Server admin choice while vanilla clients are connected: they cannot honor Ghost, so the server may refuse it.</summary>
    public enum GhostVanillaPolicy { Allow, Refuse }

    public static class GhostVanillaRule
    {
        /// <summary>Default keeps the original behavior: Ghost stays available, partial for vanilla-run creatures.</summary>
        public const GhostVanillaPolicy Default = GhostVanillaPolicy.Allow;

        /// <summary>True when the setting refuses Ghost confirmations now.</summary>
        public static bool Blocks(GhostVanillaPolicy policy, bool vanillaClientsConnected) => policy == GhostVanillaPolicy.Refuse && vanillaClientsConnected;

        /// <summary>
        /// Whether vanilla clients count as connected, from Crucible's <c>meadworks.peers.vanillacount</c> answer. A known
        /// count answers as reported. An unknown count (capability absent, -1 or throwing) is conservative: under Refuse a
        /// vanilla client may be connected; under Allow the answer cannot change anything, so it reads as none.
        /// </summary>
        public static bool VanillaPresent(GhostVanillaPolicy policy, bool known, bool anyVanilla) => known ? anyVanilla : policy == GhostVanillaPolicy.Refuse;

        /// <summary>Pending Crucible handshakes are not vanilla; confirmed vanilla and Crucible peers without Hex are.</summary>
        public static bool BlocksForPeer(bool pending, bool crucible, bool hasHex) => !pending && (!crucible || !hasHex);
    }

    /// <summary>
    /// Server: under Refuse a known vanilla or Hex-absent peer blocks only after it has stayed present continuously for
    /// <see cref="GraceSeconds"/>. Crucible peers still pending classification are excluded by the caller. While the
    /// window runs nothing is blocked: existing ghosts stay and new requests are confirmed by the same rule. If the
    /// peer remains through the window, all ghosts (new ones included) are revoked together. An unknown count still
    /// blocks at once under Refuse (<see cref="GhostVanillaRule.VanillaPresent"/>). Times are monotonic seconds.
    /// </summary>
    public sealed class GhostVanillaGrace
    {
        /// <summary>Well above a handshake round trip, well under Crucible's 30 s answer timeout.</summary>
        public const double GraceSeconds = 5;
        double since = double.NaN;

        public bool Blocks(GhostVanillaPolicy policy, bool known, bool anyVanilla, double now)
        {
            if (policy != GhostVanillaPolicy.Refuse || (known && !anyVanilla)) { since = double.NaN; return false; }
            if (!known) { since = double.NaN; return true; }
            if (double.IsNaN(since)) since = now;
            return now - since >= GraceSeconds;
        }

        public void Reset() => since = double.NaN;
    }

    /// <summary>Catalog keys the server formats (in its own language) and sends through Crucible's server notice API.</summary>
    public static class GhostNotices
    {
        public const string RevokedKey = "ghost_revoked", SettingKey = "ghost_vanilla_setting";

        public static IReadOnlyList<string> TextKeys { get; } = new[] { "ghost_refused_inactive", "ghost_refused_character", "ghost_refused_denied",
            "ghost_refused_capacity", "ghost_refused_vanilla", RevokedKey, SettingKey };

        public static string? RefusalKey(GhostReason reason) => reason switch
        {
            GhostReason.Inactive => "ghost_refused_inactive",
            GhostReason.Character => "ghost_refused_character",
            GhostReason.Denied => "ghost_refused_denied",
            GhostReason.Capacity => "ghost_refused_capacity",
            GhostReason.Vanilla => "ghost_refused_vanilla",
            _ => null,
        };

        /// <summary>The notice for one answer: refusals only; confirmation and off are silent.</summary>
        public static string? For(GhostAnswer answer) => answer.State == GhostState.Refused ? RefusalKey(answer.Reason) : null;
    }
}
