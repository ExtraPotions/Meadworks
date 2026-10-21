// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/WorldObjects.cs HeldByPlayer and src/Regrow.cs BuiltNear). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: the two tests are pure functions over facts the plugin reads from ZDOs and peers (no game
// types), and the clearance test takes the positions of player-built objects instead of searching sectors itself.
using System.Collections.Generic;

namespace ExtraPotions.Delve.Engine
{
    /// <summary>The held rule (design B4) and the build clearance rule (B5), as RestockRegen decides them.</summary>
    public static class WorldRules
    {
        /// <summary>Height window of the clearance test: a piece more than this far above or below the spot does not count.</summary>
        public const float ClearanceHeight = 30f;

        /// <summary>
        /// A player has the object loaded: its owner is a connected player whose active area covers it. An owner alone does
        /// not say so: the server only takes ownership back from objects near a connected player, so an object keeps the
        /// session id of the last player who was near it after they log off or walk away.
        /// </summary>
        public static bool HeldByPlayer(bool hasOwner, bool ownerIsServer, bool ownerConnected, bool inOwnerActiveArea) =>
            hasOwner && !ownerIsServer && ownerConnected && inOwnerActiveArea;

        /// <summary>
        /// Anything a player built (an object with a creator) within <paramref name="clearance"/> metres, flat, of the spot,
        /// and within <see cref="ClearanceHeight"/> metres of its height. A clearance of 0 or less turns the test off.
        /// </summary>
        public static bool BuiltNear(Vec3 spot, float clearance, IEnumerable<Vec3> playerBuilt)
        {
            if (clearance <= 0f || playerBuilt == null) return false;
            foreach (var p in playerBuilt)
                if (Vec3.Flat(p, spot) < clearance && System.Math.Abs(p.Y - spot.Y) < ClearanceHeight) return true;
            return false;
        }
    }
}
