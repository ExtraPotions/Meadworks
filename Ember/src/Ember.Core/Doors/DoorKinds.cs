// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), MIT. Adapted from DoorOpenerBruh 2.0.10 at
// dafb2af2418710dd1f87907e8b30d4efb1b68a20, DoorOpenerBruh/Assets/Factories/DoorFactory.cs (prefab to kind table),
// DoorOpenerBruh/Components/DoorOpener.cs (AutomationMechanic) and the 23 classes in
// DoorOpenerBruh/Assets/Pieces/Doors/*.cs (per-kind default mechanic, distances and Check for Key).
// The MIT notice is retained in THIRD_PARTY_NOTICES/DoorOpenerBruh-MIT.txt.
// Changed by expDARE, 2026: the 23 door classes collapsed into one data table in design section 4.3 order; config
// binding moved to the plugin (E02); namespace Ember. Deep North (Valheim 1.0.20): piece_drawbridge uses the game token
// $piece_drawbridge_dn and World: Morkhalla Drawbridge is added (not a DoorOpenerBruh kind).
// October 20, 2026 playtest: Open and Close Distance default to half the per-kind distances in the table; those
// table values are the earlier defaults, used once to move untouched saved values.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Ember.Doors
{
    /// <summary>Which doors a kind opens automatically (names kept from DoorOpenerBruh's config values).</summary>
    public enum AutomationMechanic
    {
        OnlyOpenSelfmadeDoors,
        OnlyOpenAllPlayerMadeDoors,
        OpenAllDoors,
        DoNotOpenAutomatically,
    }

    /// <summary>One door kind: its config section, prefab, game piece token and defaults.</summary>
    public sealed class DoorKind
    {
        public string Section { get; }
        public string Prefab { get; }
        public string PieceToken { get; }
        public AutomationMechanic DefaultMechanic { get; }
        /// <summary>Half the earlier default (October 20, 2026 playtest): 1.5 m for doors, 3 m for drawbridges.</summary>
        public float DefaultOpenDistance => PreviousOpenDistance * DoorDistances.DefaultScale;

        /// <summary>Half the earlier default: 2.25 m for doors, 4 m for drawbridges.</summary>
        public float DefaultCloseDistance => PreviousCloseDistance * DoorDistances.DefaultScale;

        /// <summary>The earlier Open Distance default; a saved value equal to it moves to the new default once.</summary>
        public float PreviousOpenDistance { get; }

        /// <summary>The earlier Close Distance default; a saved value equal to it moves to the new default once.</summary>
        public float PreviousCloseDistance { get; }

        /// <summary>The kind has a Check for Key option (doors with a key item may open when the player has the key).</summary>
        public bool HasCheckForKey { get; }

        public bool DefaultCheckForKey => false;

        /// <summary>The game's English piece name when the config section uses a different label; null otherwise.</summary>
        public string? GameName { get; }

        internal DoorKind(string section, string prefab, string pieceToken, AutomationMechanic mechanic, float previousOpen, float previousClose, bool hasCheckForKey, string? gameName = null)
        {
            Section = section;
            Prefab = prefab;
            PieceToken = pieceToken;
            DefaultMechanic = mechanic;
            PreviousOpenDistance = previousOpen;
            PreviousCloseDistance = previousClose;
            HasCheckForKey = hasCheckForKey;
            GameName = gameName;
        }

        public override string ToString() => Section;
    }

    /// <summary>
    /// D6, D7: the door kind table (design 4.3); unknown and modded doors use Other. The two distances in each row are
    /// the earlier defaults kept for the one-time defaults move; each kind now defaults to half of them
    /// (<see cref="DoorDistances.DefaultScale"/>).
    /// </summary>
    public static class DoorKinds
    {
        public const string OtherKey = "other";

        const AutomationMechanic All_ = AutomationMechanic.OpenAllDoors;
        const AutomationMechanic Never = AutomationMechanic.DoNotOpenAutomatically;

        static readonly DoorKind[] kinds =
        {
            new DoorKind("Buildable: Wood Door", "wood_door", "$piece_wooddoor", All_, 3f, 4.5f, false),
            new DoorKind("Buildable: Wood Gate", "wood_gate", "$piece_woodgate", All_, 3f, 4.5f, false),
            new DoorKind("Buildable: Wood Fence Gate", "wood_fence_gate", "$piece_woodfencegate", All_, 3f, 4.5f, false, "Roundpole Gate"),
            new DoorKind("Buildable: Stave Gate", "stave_gate", "$piece_stavegate", All_, 3f, 4.5f, false, "Timberwood Gate"),
            new DoorKind("Buildable: Wood Shutter", "wood_window", "$piece_woodwindowshutter", All_, 3f, 4.5f, false),
            new DoorKind("Buildable: Iron Gate", "iron_grate", "$piece_irongate", All_, 3f, 4.5f, false),
            new DoorKind("Buildable: Darkwood Gate", "darkwood_gate", "$piece_darkwoodgate", All_, 3f, 4.5f, false),
            new DoorKind("Buildable: Hexagonal Gate", "piece_hexagonal_door", "$piece_hexagonalgate", All_, 3f, 4.5f, false),
            new DoorKind("Buildable: Ashwood Door", "ashwood_door", "$piece_ashwood_door", All_, 3f, 4.5f, false),
            new DoorKind("Buildable: Flametal Gate", "flametal_gate", "$piece_flametal_gate", All_, 3f, 4.5f, false),
            new DoorKind("Buildable: Grausten Door", "piece_grausten_door", "$piece_grausten_door", All_, 3f, 4.5f, false),
            new DoorKind("Buildable: Grausten Gate", "piece_grausten_gate", "$piece_grausten_gate", All_, 3f, 4.5f, false),
            // Valheim 1.0.20: the piece_drawbridge prefab is the Deep North Timberwood Drawbridge (Piece m_name $piece_drawbridge_dn).
            new DoorKind("Buildable: Drawbridge", "piece_drawbridge", "$piece_drawbridge_dn", All_, 6f, 8f, false, "Timberwood Drawbridge"),
            new DoorKind("Buildable: Rustic Drawbridge", "piece_drawbridge_log", "$piece_drawbridge_log", All_, 6f, 8f, false),
            new DoorKind("World: Mountain Wood Gate", "MountainKit_wood_gate", "$piece_woodgate", All_, 3f, 4.5f, false, "Wood Gate, placed in mountain locations"),
            new DoorKind("World: Dvergr Wood Door", "piece_dvergr_wood_door", "$piece_dvergr_door", Never, 3f, 4.5f, false),
            new DoorKind("World: Mistlands Queen Door", "dungeon_queen_door", "$piece_queendoor", Never, 3f, 4.5f, true),
            new DoorKind("World: Sunken Crypt Gate", "sunken_crypt_gate", "$piece_irongate", Never, 3f, 4.5f, true),
            new DoorKind("World: Morkhalla Drawbridge", "Morkhalla_Drawbridge", "$morkhalla_drawbridge", Never, 6f, 8f, false, "Drawbridge, placed in Deep North Morkhalla locations"),
            new DoorKind("Dungeon: Burial Chamber Door", "dungeon_forestcrypt_door", "$piece_wooddoor", Never, 3f, 4.5f, false),
            new DoorKind("Dungeon: Sunken Crypt Iron Gate", "dungeon_sunkencrypt_irongate", "$piece_irongate", Never, 3f, 4.5f, false),
            new DoorKind("Dungeon: Dvergr Town Secret Door", "dvergrtown_secretdoor", "$piece_secretdoor", Never, 3f, 4.5f, false),
            new DoorKind("Dungeon: Dvergr Town Sliding Door", "dvergrtown_slidingdoor", "$piece_dv_gate", Never, 3f, 4.5f, false),
            new DoorKind("Other: Other and Custom Doors", OtherKey, "other", AutomationMechanic.OnlyOpenAllPlayerMadeDoors, 3f, 4.5f, true),
        };

        static readonly Dictionary<string, DoorKind> byPrefab = Index();

        /// <summary>All kinds in design section 4.3 order; Other is last.</summary>
        public static IReadOnlyList<DoorKind> All { get; } = Array.AsReadOnly(kinds);

        public static DoorKind Other => kinds[kinds.Length - 1];

        /// <summary>The kind for a prefab name (exact, as the game names prefabs); Other for anything unknown.</summary>
        public static DoorKind ForPrefab(string? prefab) =>
            prefab != null && byPrefab.TryGetValue(prefab, out var kind) ? kind : Other;

        static Dictionary<string, DoorKind> Index()
        {
            var index = new Dictionary<string, DoorKind>(StringComparer.Ordinal);
            foreach (var kind in kinds)
                index.Add(kind.Prefab, kind);
            return index;
        }
    }
}
