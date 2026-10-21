// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the game members Delve patches or reads (design E5: a hook list checked after each game update), with their exact metadata signatures.
// The portable tests check every entry against assembly_valheim.dll (System.Reflection.Metadata, no game load).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Delve.Integration
{
    public enum HookKind { Method, Field }

    /// <summary>Wired: the plugin uses the member now. Pending: listed for a later native unit.</summary>
    public enum HookStatus { Wired, Pending }

    public sealed class HookTarget
    {
        internal HookTarget(string module, string type, string member, HookKind kind, string signature, HookStatus status, string purpose)
        {
            Module = module;
            Type = type;
            Member = member;
            Kind = kind;
            Signature = signature;
            Status = status;
            Purpose = purpose;
        }

        public string Module { get; }

        /// <summary>Metadata type name, nested types joined with <c>/</c>.</summary>
        public string Type { get; }

        public string Member { get; }

        public HookKind Kind { get; }

        /// <summary>Methods: comma-joined parameter types; fields: the field type. Metadata short names, nested with <c>/</c>, by-ref with <c>&amp;</c>.</summary>
        public string Signature { get; }

        public HookStatus Status { get; }

        public string Purpose { get; }

        public override string ToString() => Type + "." + Member + (Kind == HookKind.Method ? "(" + Signature + ")" : " : " + Signature);
    }

    public static class HookTargets
    {
        private static HookTarget M(string module, string type, string member, string parameters, HookStatus status, string purpose) =>
            new HookTarget(module, type, member, HookKind.Method, parameters, status, purpose);

        private static HookTarget F(string module, string type, string member, string fieldType, HookStatus status, string purpose) =>
            new HookTarget(module, type, member, HookKind.Field, fieldType, status, purpose);

        public static IReadOnlyList<HookTarget> All { get; } = Array.AsReadOnly(new[]
        {
            M("Rooms", "DungeonGenerator", "GenerateRooms", "ZoneSystem/SpawnMode", HookStatus.Wired, "prefix sets min and max rooms, postfix restores them (A1)"),
            F("Rooms", "DungeonGenerator", "m_minRooms", "Int32", HookStatus.Wired, "the generator's own min rooms"),
            F("Rooms", "DungeonGenerator", "m_maxRooms", "Int32", HookStatus.Wired, "the generator's own max rooms"),
            F("Rooms", "DungeonGenerator", "m_algorithm", "DungeonGenerator/Algorithm", HookStatus.Wired, "only the Dungeon algorithm is sized"),
            M("FullReset", "DungeonGenerator", "Generate", "ZoneSystem/SpawnMode", HookStatus.Wired, "generate a wiped interior again in ghost mode (DV09)"),
            F("FullReset", "DungeonGenerator", "m_zoneSize", "Vector3", HookStatus.Wired, "the interior volume (DV09)"),
            M("FullReset", "ZNetView", "StartGhostInit", "", HookStatus.Wired, "ghost spawning as ZoneSystem does (DV09)"),
            M("FullReset", "ZNetView", "FinishGhostInit", "", HookStatus.Wired, "ghost spawning as ZoneSystem does (DV09)"),
            M("FullReset", "ZoneSystem", "GetZonePos", "Vector2s", HookStatus.Wired, "the interior's zone center (DV09)"),
            M("Surface", "ZoneSystem", "PlaceVegetation", "Vector2s,Vector3,Transform,Heightmap,List`1<ZoneSystem/ClearArea>,ZoneSystem/SpawnMode,List`1<GameObject>", HookStatus.Wired, "replay a generated zone's vegetation in ghost mode (parity 26, 27)"),
            M("Surface", "ZoneSystem", "IsZoneGenerated", "Vector2s", HookStatus.Wired, "only generated zones are reset (parity 26, 27)"),
            F("Surface", "ZoneSystem", "m_zones", "Dictionary`2<Vector2s,ZoneSystem/ZoneData>", HookStatus.Wired, "a loaded zone's own terrain (parity 26, 27)"),
            F("Surface", "ZoneSystem", "m_zonePrefab", "GameObject", HookStatus.Wired, "terrain for a zone no one has loaded on the server (parity 26, 27)"),
            F("Surface", "ZoneSystem", "m_locationInstances", "Dictionary`2<Vector2s,ZoneSystem/LocationInstance>", HookStatus.Wired, "location clear areas and exteriors (parity 26, 27)"),
            M("Surface", "HeightmapBuilder", "IsTerrainReady", "Vector3,Int32,Single,Boolean,WorldGenerator", HookStatus.Wired, "wait for a zone's terrain (parity 26, 27)"),
            F("Surface", "ZNetScene", "m_instances", "Dictionary`2<ZDO,ZNetView>", HookStatus.Wired, "loaded objects stand aside during the replay (parity 26, 27)"),
            F("Surface", "TreeBase", "m_stubPrefab", "GameObject", HookStatus.Wired, "clear a stump where a tree returns (parity 26, 27)"),
            F("Surface", "Destructible", "m_spawnWhenDestroyed", "GameObject", HookStatus.Wired, "a partly mined rock is the same rock (parity 26, 27)"),
            M("Commands", "ZNet", "RPC_Kick", "ZRpc,String", HookStatus.Wired, "kick fallback: delve-* words from a remote admin (DV11)"),
            M("Commands", "ZNet", "Kick", "String", HookStatus.Wired, "kick fallback: the host's or single player's own kick (DV11)"),
            M("Commands", "ZNet", "RemotePrint", "ZRpc,String", HookStatus.Wired, "reply in the admin's console (DV11)"),
            M("Engine", "EnvMan", "GetDay", "Double", HookStatus.Pending, "in-game day for clocks (DV03b)"),
            M("Engine", "ZNet", "GetTimeSeconds", "", HookStatus.Wired, "world time for fractional days and dawn (DV03b)"),
            F("Engine", "EnvMan", "m_dayLengthSec", "Int64", HookStatus.Wired, "day length for the world day (DV03b)"),
            M("Engine", "SaveSystem", "GetWorldsSaveRootPath", "FileHelpers/FileSource", HookStatus.Wired, "sidecar folder worlds_local (DV03b)"),
            M("Engine", "ZDOMan", "FindSectorObjects", "Vector2s,SimulationDistance,List`1<ZDO>,List`1<ZDO>", HookStatus.Wired, "objects near a site for clearance and bases (DV03b)"),
            F("Engine", "PrivateArea", "m_radius", "Single", HookStatus.Wired, "ward radius for the base guard (B6)"),
            F("Engine", "EffectArea", "m_type", "EffectArea/Type", HookStatus.Wired, "player base areas for the base guard (B6)"),
            F("Engine", "ZDOVars", "s_enabled", "Int32", HookStatus.Wired, "a disabled ward does not guard"),
            M("Engine", "ZoneSystem", "GetZone", "Vector3", HookStatus.Wired, "visit zones and nearby objects (DV06b)"),
            M("Engine", "ZNetScene", "InActiveArea", "Vector3,Vector3", HookStatus.Wired, "held rule (DV03b)"),
            M("Engine", "ZDOMan", "DestroyZDO", "ZDO", HookStatus.Wired, "remove a damaged object (DV06b)"),
            M("Engine", "ZDOMan", "CreateNewZDO", "Vector3,Int32", HookStatus.Wired, "create a restored object (DV06b)"),
            M("Chests", "ZDO", "Deserialize", "ZPackage", HookStatus.Wired, "openings and first sight (DV05b, DV06b)"),
            M("Chests", "Container", "SetInUse", "Boolean", HookStatus.Wired, "the host's own chest opening and closing (R-Q4)"),
            M("Chests", "Container", "Save", "", HookStatus.Wired, "the host's own chest emptying (R-Q4)"),
            F("Chests", "Container", "m_defaultItems", "DropTable", HookStatus.Wired, "a loot chest has a loot table (DV05b)"),
            F("Chests", "Container", "m_width", "Int32", HookStatus.Wired, "refill inventory size"),
            F("Chests", "Container", "m_height", "Int32", HookStatus.Wired, "refill inventory size"),
            M("Chests", "DropTable", "GetDropListItems", "", HookStatus.Wired, "roll the chest's own loot table"),
            M("Chests", "Inventory", "Save", "ZPackage", HookStatus.Wired, "write the fresh roll as the game does"),
            F("Chests", "ZDOVars", "s_items", "Int32", HookStatus.Wired, "chest inventory"),
            F("Chests", "ZDOVars", "s_inUse", "Int32", HookStatus.Wired, "chest open"),
            F("Chests", "ZDOVars", "s_creator", "Int32", HookStatus.Wired, "player-built objects"),
            F("Resources", "ZDOVars", "s_picked", "Int32", HookStatus.Wired, "unpick black cores and Morkhalla eyes"),
            F("Resources", "ZDOVars", "s_pickedTime", "Int32", HookStatus.Wired, "clear the picked time with the mark"),
            F("Resources", "ZDOVars", "s_roomData", "Int32", HookStatus.Wired, "the Queen's room from the generator's saved rooms (DV08)"),
            F("Resources", "ZDOVars", "s_state", "Int32", HookStatus.Wired, "close the citadel's sealed door (DV08)"),
            F("Resources", "ZDOVars", "s_location", "Int32", HookStatus.Wired, "find the Deep North boss room by its location proxy (Kall's return)"),
            F("Resources", "ZDOVars", "s_dead", "Int32", HookStatus.Wired, "a flametal spire that is leaving"),
            M("Resources", "WorldGenerator", "GetBiome", "Vector3", HookStatus.Wired, "biome entry notices (DV07)"),
            F("Respawn", "CreatureSpawner", "m_respawnTimeMinuts", "Single", HookStatus.Wired, "arm a one-shot spawner (DV15b)"),
            M("Respawn", "CreatureSpawner", "HasSpawned", "", HookStatus.Wired, "only spawners that spawned before"),
            M("Respawn", "CreatureSpawner", "Awake", "", HookStatus.Wired, "arm on load (Elite Creatures Reborn hook)"),
            M("Respawn", "CreatureSpawner", "Spawn", "", HookStatus.Wired, "arm after the first spawn"),
            M("Respawn", "ZDO", "GetConnection", "", HookStatus.Wired, "spawned state and the creature it points to (server reset)"),
            F("Respawn", "ZDOVars", "s_aliveTime", "Int32", HookStatus.Wired, "last time the spawner's creature lived (server reset)"),
            F("Respawn", "ZDOVars", "s_spawnTime", "Int32", HookStatus.Pending, "spawned state (server reset)"),
        });

        public static IEnumerable<HookTarget> Wired => All.Where(t => t.Status == HookStatus.Wired);
    }
}
