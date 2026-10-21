// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: who may count a sea-gate landing, who may clear a saved pair, and what a refused crew teleport does.

namespace ExtraPotions.Threshold.SeaGates
{
    /// <summary>What the crew client does after <c>Player.TeleportTo</c>. A refusal forgets the jump, tells the ship, and
    /// leaves the helm alone. A start keeps the jump and releases the helm.</summary>
    public readonly struct CrewTeleportDecision
    {
        public CrewTeleportDecision(int lastJump, bool decline, bool releaseHelm)
        {
            LastJump = lastJump;
            Decline = decline;
            ReleaseHelm = releaseHelm;
        }

        /// <summary>The jump id to remember. 0 forgets it, so the same order can be offered again.</summary>
        public int LastJump { get; }

        /// <summary>Tell the ship this player is not coming.</summary>
        public bool Decline { get; }

        /// <summary>Let go of the helm. False on a refusal, so a failed teleport does not stand them up first.</summary>
        public bool ReleaseHelm { get; }

        public static CrewTeleportDecision AfterAttempt(int jumpId, bool teleported) =>
            teleported
                ? new CrewTeleportDecision(jumpId, false, true)
                : new CrewTeleportDecision(0, true, false);
    }

    /// <summary>Sender checks for sea-gate RPCs. The ship or pillar owner applies them. A landing is not a distance
    /// check: the player may already be ashore, or still in the teleport, far from the ship.</summary>
    public static class SeaGateAuthority
    {
        /// <summary>Count a landing when the server sent it, or the sender owns the reported player. A player id of 0
        /// never counts. Owning them is either their loaded character's owner, or this peer's own player id.</summary>
        public static bool AcceptsLanding(bool fromServer, long sender, long senderPlayerId, long reportedPlayerId, long reportedOwner)
        {
            if (reportedPlayerId == 0)
                return false;
            if (fromServer)
                return true;
            if (reportedOwner != 0 && sender == reportedOwner)
                return true;
            return senderPlayerId != 0 && senderPlayerId == reportedPlayerId;
        }

        /// <summary>Clear a saved pair when the server sent it, or the sender owns the partner pillar. An unknown owner
        /// (the partner is not loaded here) does not clear the link.</summary>
        public static bool AcceptsUnpair(bool fromServer, long sender, long partnerOwner) =>
            fromServer || (partnerOwner != 0 && sender == partnerOwner);
    }
}
