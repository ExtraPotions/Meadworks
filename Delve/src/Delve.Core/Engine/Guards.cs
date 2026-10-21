// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the guards every kind passes before anything comes back (design B3 to B6), in one order.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Delve.Engine
{
    /// <summary>Why a due site waits; Clear lets it act.</summary>
    public enum GuardResult { Clear, PlayerNear, Held, Built, PlayerBase }

    /// <summary>What the guards need to know about one site now. The plugin fills it from the world.</summary>
    public sealed class GuardInput
    {
        public GuardInput(Vec3 center, float playerRadius)
        {
            if (!(playerRadius >= 0) || float.IsInfinity(playerRadius)) throw new ArgumentException("Expected a finite radius of 0 or more.", nameof(playerRadius));
            Center = center;
            PlayerRadius = playerRadius;
        }

        /// <summary>The site's spot, or a dungeon's entrance.</summary>
        public Vec3 Center { get; }

        /// <summary>A player this close (flat) holds the site (design B3; RR entrance and visit radii).</summary>
        public float PlayerRadius { get; }

        /// <summary>A dungeon's interior volume: a player inside it holds the site wherever the entrance is.</summary>
        public Box? Interior { get; set; }

        public IReadOnlyList<Vec3> Players { get; set; } = Array.Empty<Vec3>();

        /// <summary>The object to replace is held by a connected player (B4, <see cref="WorldRules.HeldByPlayer"/>).</summary>
        public bool HeldByPlayer { get; set; }

        /// <summary>Build clearance in meters (B5); 0 turns it off.</summary>
        public float Clearance { get; set; }

        /// <summary>Positions of player-built objects near the site.</summary>
        public IReadOnlyList<Vec3> Built { get; set; } = Array.Empty<Vec3>();

        /// <summary>Player base guard on (B6, setting General / Player Base Guard).</summary>
        public bool BaseGuard { get; set; } = true;

        /// <summary>Player base and ward areas near the site.</summary>
        public IReadOnlyList<Area> Bases { get; set; } = Array.Empty<Area>();
    }

    public static class Guards
    {
        /// <summary>The first guard that holds the site: player near, held, built, player base; else Clear.</summary>
        public static GuardResult Evaluate(GuardInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            foreach (var p in input.Players)
                if (Vec3.Flat(p, input.Center) < input.PlayerRadius || (input.Interior.HasValue && input.Interior.Value.Contains(p)))
                    return GuardResult.PlayerNear;
            if (input.HeldByPlayer) return GuardResult.Held;
            if (WorldRules.BuiltNear(input.Center, input.Clearance, input.Built)) return GuardResult.Built;
            if (input.BaseGuard)
                foreach (var area in input.Bases)
                    if (area.Contains(input.Center)) return GuardResult.PlayerBase;
            return GuardResult.Clear;
        }
    }
}
