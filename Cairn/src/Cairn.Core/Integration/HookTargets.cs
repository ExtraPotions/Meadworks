// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the game members Cairn patches or reads (design section 7), with their exact metadata signatures.
// The portable tests check every entry against assembly_valheim.dll (System.Reflection.Metadata, no game load).
using System;
using System.Collections.Generic;

namespace ExtraPotions.Cairn.Integration
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
        /// <summary>CA05b members in Valheim's separate assembly_postprocessing.dll, checked without loading Unity.</summary>
        public static IReadOnlyList<HookTarget> PostProcessing { get; } = Array.AsReadOnly(new[]
        {
            M("Visibility", "PostProcessingBehaviour", "OnPreRender", "", HookStatus.Wired, "native post-processing command-buffer execution"),
            M("Visibility", "PostProcessingBehaviour", "TryExecuteCommandBuffer", "PostProcessingComponentCommandBuffer`1<!!0>", HookStatus.Wired, "preserved native execution when removal is off"),
            M("Visibility", "PostProcessingBehaviour", "RemoveCommandBuffer", "", HookStatus.Wired, "NoFogBruh fog and ambient command-buffer removal"),
        });

        private static HookTarget M(string module, string type, string member, string parameters, HookStatus status, string purpose) =>
            new HookTarget(module, type, member, HookKind.Method, parameters, status, purpose);

        private static HookTarget F(string module, string type, string member, string fieldType, HookStatus status, string purpose) =>
            new HookTarget(module, type, member, HookKind.Field, fieldType, status, purpose);

        public static IReadOnlyList<HookTarget> All { get; } = Array.AsReadOnly(new[]
        {
            M("Radius", "Minimap", "UpdateExplore", "Single,Player", HookStatus.Wired, "transpiler replaces the m_exploreRadius read with Cairn's radius (A7); prefix and finalizer mark the natural explore scope for Exploration experience (CA04b)"),
            F("Radius", "Minimap", "m_exploreRadius", "Single", HookStatus.Wired, "the field the transpiler replaces; also the radius-off base"),
            M("Radius", "Character", "InInterior", "", HookStatus.Wired, "dungeon radius (Pathfinder interior rule)"),
            M("Radius", "Player", "GetPlayersInRange", "Vector3,Single,List`1<Player>", HookStatus.Wired, "players within 21 m"),
            M("Radius", "Player", "IsAttachedToShip", "", HookStatus.Wired, "sea radius"),
            M("Radius", "Player", "GetCurrentBiome", "", HookStatus.Wired, "location term"),
            M("Radius", "WorldGenerator", "InForest", "Vector3", HookStatus.Wired, "Meadows forest"),
            M("Radius", "WorldGenerator", "GetForestFactor", "Vector3", HookStatus.Wired, "Plains forest"),
            F("Radius", "ZoneSystem", "m_waterLevel", "Single", HookStatus.Wired, "altitude"),
            F("Radius", "EnvMan", "m_dirLight", "Light", HookStatus.Wired, "daylight term"),
            M("Radius", "EnvMan", "GetCurrentEnvironment", "", HookStatus.Wired, "weather particle systems"),
            F("Radius", "EnvSetup", "m_psystems", "GameObject[]", HookStatus.Wired, "Mist and SnowStorm names"),
            M("Radius", "Ship", "GetLocalShip", "", HookStatus.Wired, "GrindstoneSkills Sailing factor only while aboard"),
            M("Visibility", "EnvMan", "SetEnv", "EnvSetup,Single,Single,Single,Single,Single", HookStatus.Wired, "game fog density captured, distance fog removed, scene targets applied"),
            M("Visibility", "EnvMan", "Update", "", HookStatus.Wired, "snow glint removed each frame"),
            M("Visibility", "EnvMan", "SetParticleArrayEnabled", "GameObject[],Boolean", HookStatus.Wired, "mist emitters and distant fog emitters"),
            M("Visibility", "ParticleMist", "Update", "", HookStatus.Wired, "particle mist skipped and cleared"),
            F("Visibility", "ParticleMist", "m_ps", "ParticleSystem", HookStatus.Wired, "particle mist system"),
            M("Visibility", "DistantFogEmitter", "Update", "", HookStatus.Wired, "distant fog stops emitting"),
            M("Visibility", "DistantFogEmitter", "SetEmit", "Boolean", HookStatus.Wired, "distant fog stops emitting"),
            M("Visibility", "Smoke", "GetAlpha", "", HookStatus.Wired, "smoke alpha 0 or scaled by Smoke Strength (BetterFog hook)"),
            F("Visibility", "DistantFogEmitter", "m_psystems", "ParticleSystem[]", HookStatus.Wired, "fog bank systems graded by Mist Strength"),
            F("Visibility", "EnvMan", "m_currentPSystems", "GameObject[]", HookStatus.Wired, "weather fog-like systems graded by Mist Strength"),
            M("Visibility", "LocationList", "GetAllLocationLists", "", HookStatus.Wired, "biome location lists for Mistlands, cave, Ashlands and Deep North mist"),
            M("Hud", "EnvMan", "GetDayFraction", "", HookStatus.Wired, "clock time (CA06b)"),
            M("Hud", "EnvMan", "GetCurrentDay", "", HookStatus.Wired, "day number (CA06b)"),
            M("Hud", "EnvMan", "IsNight", "", HookStatus.Wired, "night fog density for the weather fallback (CA06b)"),
            F("Hud", "EnvSetup", "m_name", "String", HookStatus.Wired, "weather category by environment name (CA06b)"),
            F("Hud", "EnvSetup", "m_isWet", "Boolean", HookStatus.Wired, "rain fallback for unknown environments (CA06b)"),
            F("Hud", "EnvSetup", "m_fogDensityDay", "Single", HookStatus.Wired, "fog fallback for unknown environments (CA06b)"),
            F("Hud", "EnvSetup", "m_fogDensityNight", "Single", HookStatus.Wired, "fog fallback for unknown environments (CA06b)"),
            M("Hud", "Hud", "IsUserHidden", "", HookStatus.Wired, "hides with the game's HUD toggle (CA06b)"),
            M("Hud", "Minimap", "IsOpen", "", HookStatus.Wired, "hidden on the large map (CA06b)"),
            M("Hud", "Menu", "UpdateNavigation", "", HookStatus.Wired, "owned pause-menu entry navigation (CA07b)"),
            F("Hud", "Menu", "menuEntriesParent", "RectTransform", HookStatus.Wired, "owned settings entry parent (CA07b)"),
            F("Hud", "Menu", "m_settingsButton", "Button", HookStatus.Wired, "native kit menu-button template (CA07b)"),
            M("Skill", "Minimap", "Explore", "Vector3,Single", HookStatus.Wired, "prefix counts newly explored texels inside the natural or Vantage scope; Vantage calls it (CA04b)"),
            F("Skill", "Minimap", "m_explored", "BitArray", HookStatus.Wired, "the local player's explored texels, read before an explore"),
            M("Skill", "Minimap", "WorldToPixel", "Vector3,Int32&,Int32&", HookStatus.Wired, "explore centre texel"),
            F("Skill", "Minimap", "m_pixelSize", "Single", HookStatus.Wired, "texel size in meters for area experience"),
            F("Skill", "Minimap", "m_textureSize", "Int32", HookStatus.Wired, "explored map width in texels"),
            M("Skill", "Minimap", "DiscoverLocation", "Vector3,Minimap/PinType,String,Boolean", HookStatus.Wired, "postfix: discovery experience when a new pin was added inside a natural response (CA04b)"),
            M("Skill", "Vegvisir", "Interact", "Humanoid,Boolean,Boolean", HookStatus.Wired, "natural discovery interaction scope"),
            M("Skill", "RuneStone", "Interact", "Humanoid,Boolean,Boolean", HookStatus.Wired, "natural discovery interaction scope"),
            M("Skill", "Game", "DiscoverClosestLocation", "String,Vector3,String,Int32,Boolean,Boolean", HookStatus.Wired, "records the natural request sent to the server"),
            M("Skill", "Game", "RPC_DiscoverLocationResponse", "Int64,String,Int32,Vector3,Boolean", HookStatus.Wired, "natural response scope around the pin it adds"),
            M("Skill", "Player", "AddKnownBiome", "BiomeSector", HookStatus.Wired, "prefix reads IsBiomeKnown, postfix pays new biome experience (CA04b)"),
            M("Skill", "Player", "IsBiomeKnown", "BiomeSector", HookStatus.Wired, "biome known before AddKnownBiome"),
            M("Skill", "Character", "IsOnGround", "", HookStatus.Wired, "Vantage stands on the ground"),
            M("Skill", "Character", "GetVelocity", "", HookStatus.Wired, "Vantage stands still"),
            M("Skill", "Player", "Message", "MessageHud/MessageType,String,Int32,Sprite,Boolean", HookStatus.Wired, "Vantage notice"),
            M("Skill", "ZNetScene", "GetPrefab", "String", HookStatus.Wired, "map table prefab for the skill icon"),
            F("Skill", "Piece", "m_icon", "Sprite", HookStatus.Wired, "map table icon, read at run time (D1)"),
            F("Markers", "Minimap", "m_pins", "List`1<Minimap/PinData>", HookStatus.Wired, "the player's map pins for the Markers map (CA07a)"),
            M("Markers", "Minimap", "GetSprite", "Minimap/PinType", HookStatus.Wired, "the game's own pin icons on the Markers map"),
            M("Markers", "Minimap", "WorldToMapPoint", "Vector3,Single&,Single&", HookStatus.Wired, "pin positions on the Markers map"),
            F("Markers", "Minimap", "m_mapTexture", "Texture2D", HookStatus.Wired, "Markers map image, the game's own at runtime"),
            F("Markers", "Minimap", "m_exploredOthers", "BitArray", HookStatus.Wired, "Markers map fog of war: shared map data"),
            F("Markers", "Minimap", "m_showSharedMapData", "Boolean", HookStatus.Wired, "shared map data shown, as on the large map"),
            F("Markers", "Minimap", "m_heightTexture", "Texture2D", HookStatus.Wired, "Markers map water below the water level"),
            F("Markers", "Minimap", "m_meadowsColor", "Color", HookStatus.Wired, "biome tint in the game's map texture"),
            F("Markers", "Minimap", "m_blackforestColor", "Color", HookStatus.Wired, "biome tint in the game's map texture"),
            F("Markers", "Minimap", "m_swampColor", "Color", HookStatus.Wired, "biome tint in the game's map texture"),
            F("Markers", "Minimap", "m_mountainColor", "Color", HookStatus.Wired, "biome tint in the game's map texture"),
            F("Markers", "Minimap", "m_heathColor", "Color", HookStatus.Wired, "biome tint in the game's map texture (plains)"),
            F("Markers", "Minimap", "m_mistlandsColor", "Color", HookStatus.Wired, "biome tint in the game's map texture"),
            F("Markers", "Minimap", "m_ashlandsColor", "Color", HookStatus.Wired, "biome tint in the game's map texture"),
            F("Markers", "Minimap", "m_deepnorthColor", "Color", HookStatus.Wired, "biome tint in the game's map texture"),
            F("Markers", "Minimap/PinData", "m_type", "Minimap/PinType", HookStatus.Wired, "pin type, the marker type entry; Tidy Pins keeps player, ping and shout pins at the game's size and opacity"),
            F("Markers", "Minimap/PinData", "m_pos", "Vector3", HookStatus.Wired, "pin position, the single marker entry"),
            F("Markers", "Minimap/PinData", "m_name", "String", HookStatus.Wired, "pin label"),
            F("Markers", "Minimap/PinData", "m_icon", "Sprite", HookStatus.Wired, "the pin's own icon (location pins such as the start temple have type None)"),
            F("Markers", "Minimap", "m_pinSizeLarge", "Single", HookStatus.Wired, "the large map's pin size, matched by the Markers map's pins"),
            F("Markers", "Player", "m_customData", "Dictionary`2<String,String>", HookStatus.Wired, "per-world marker choices in character data (C4)"),
            M("Markers", "ZNet", "GetWorldUID", "", HookStatus.Wired, "the world the marker choices belong to"),
            M("Import", "ZNet", "GetWorld", "", HookStatus.Wired, "match imported world names to the current world"),
            F("Import", "World", "m_name", "String", HookStatus.Wired, "source marker world name"),
            M("Import", "Player", "GetPlayerName", "", HookStatus.Wired, "source marker character name"),
            M("TidyPins", "Minimap", "Update", "", HookStatus.Wired, "restore owned visuals before native refresh, then apply local tidy-pin presentation"),
            F("TidyPins", "Minimap", "m_mode", "Minimap/MapMode", HookStatus.Wired, "names are hovered only on the large map"),
            F("TidyPins", "Minimap/PinData", "m_uiElement", "RectTransform", HookStatus.Wired, "visible icon scale and mouse/pad hit area"),
            F("TidyPins", "Minimap/PinData", "m_iconElement", "Image", HookStatus.Wired, "fade checked pin icons without changing pin data"),
            F("TidyPins", "Minimap/PinData", "m_checkedElement", "GameObject", HookStatus.Wired, "fade the checked indicator alongside its icon"),
            F("TidyPins", "Minimap/PinData", "m_checked", "Boolean", HookStatus.Wired, "read-only crossed-out state"),
            F("TidyPins", "Minimap/PinData", "m_worldSize", "Single", HookStatus.Wired, "leave event-area circles at their true world size"),
        });
    }
}
