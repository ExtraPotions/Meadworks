// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), MIT. Adapted from DoorOpenerBruh 2.0.10 at
// dafb2af2418710dd1f87907e8b30d4efb1b68a20, DoorOpenerBruh/Assets/Pieces/DoorPiece.cs (ComputeAutomation,
// IsPlayerMadeDoor, IsSelfMadeDoor, DefaultDoorEnablement, DetermineCheckForKey).
// The MIT notice is retained in THIRD_PARTY_NOTICES/DoorOpenerBruh-MIT.txt.
// Changed by expDARE, 2026: game facts passed in as a DoorFacts value (the plugin reads ZDOVars.s_creator,
// Door.m_keyItem, Door.HaveKey and Door.CanInteract); self-made requires a non-zero creator; mechanic name parsing for
// the converter; namespace Ember.
using System;

namespace ExtraPotions.Ember.Doors
{
    /// <summary>Game facts about one door and the local player, read by the plugin each check.</summary>
    public readonly struct DoorFacts
    {
        /// <summary>Doors.Auto Doors Allowed (server) and Doors.Enable Auto Door (local) are both on.</summary>
        public bool MasterEnabled { get; }

        /// <summary>The door is a placement preview (Piece.s_ghostLayer): never automated (D8).</summary>
        public bool IsGhost { get; }

        /// <summary>The game's Door.CanInteract.</summary>
        public bool CanInteract { get; }

        /// <summary>ZDOVars.s_creator; 0 for world-generated doors.</summary>
        public long CreatorId { get; }

        public long LocalPlayerId { get; }
        public bool HasKeyItem { get; }
        public bool PlayerHasKey { get; }

        public DoorFacts(bool masterEnabled, bool isGhost, bool canInteract, long creatorId, long localPlayerId, bool hasKeyItem, bool playerHasKey)
        {
            MasterEnabled = masterEnabled;
            IsGhost = isGhost;
            CanInteract = canInteract;
            CreatorId = creatorId;
            LocalPlayerId = localPlayerId;
            HasKeyItem = hasKeyItem;
            PlayerHasKey = playerHasKey;
        }
    }

    /// <summary>D3, D6: whether the automation may open a door for the local player.</summary>
    public static class DoorRules
    {
        /// <summary>
        /// Key rule: a door without a key item is allowed; a door with a key item is allowed only when its kind has the
        /// Check for Key option, the option is on and the player has the key (keys in a Mantle backpack count, D12).
        /// </summary>
        public static bool KeyAllows(bool hasKeyItem, bool kindHasCheckForKey, bool checkForKey, bool playerHasKey)
        {
            if (!hasKeyItem)
                return true;
            return kindHasCheckForKey && checkForKey && playerHasKey;
        }

        public static bool AutomationEnabled(AutomationMechanic mechanic, bool kindHasCheckForKey, bool checkForKey, DoorFacts facts)
        {
            bool baseline = facts.MasterEnabled && !facts.IsGhost && facts.CanInteract
                && KeyAllows(facts.HasKeyItem, kindHasCheckForKey, checkForKey, facts.PlayerHasKey);
            if (!baseline)
                return false;
            switch (mechanic)
            {
                case AutomationMechanic.OpenAllDoors:
                    return true;
                case AutomationMechanic.OnlyOpenSelfmadeDoors:
                    return facts.CreatorId != 0L && facts.CreatorId == facts.LocalPlayerId;
                case AutomationMechanic.OnlyOpenAllPlayerMadeDoors:
                    return facts.CreatorId != 0L;
                default:
                    return false;
            }
        }

        /// <summary>Parses a mechanic name as written in a DoorOpenerBruh config (case-insensitive, trimmed, names only).</summary>
        public static bool TryParseMechanic(string? text, out AutomationMechanic mechanic)
        {
            mechanic = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var trimmed = text!.Trim();
            foreach (AutomationMechanic value in Enum.GetValues(typeof(AutomationMechanic)))
            {
                if (string.Equals(value.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    mechanic = value;
                    return true;
                }
            }
            return false;
        }
    }
}
