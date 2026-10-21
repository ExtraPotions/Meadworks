// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the game members Threshold will patch or read (design section 7, Wayfare's discovery and sync
// members), with their exact metadata signatures. The portable tests check every entry against assembly_valheim.dll
// (System.Reflection.Metadata, no game load). Same table shape as Edge.Core.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Threshold.Integration
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
        private static HookTarget M(string module, string type, string member, string parameters, string purpose) =>
            new HookTarget(module, type, member, HookKind.Method, parameters, HookStatus.Pending, purpose);

        private static HookTarget F(string module, string type, string member, string fieldType, string purpose) =>
            new HookTarget(module, type, member, HookKind.Field, fieldType, HookStatus.Pending, purpose);

        private static HookTarget WM(string module, string type, string member, string parameters, string purpose) =>
            new HookTarget(module, type, member, HookKind.Method, parameters, HookStatus.Wired, purpose);

        private static HookTarget WF(string module, string type, string member, string fieldType, string purpose) =>
            new HookTarget(module, type, member, HookKind.Field, fieldType, HookStatus.Wired, purpose);

        public static IReadOnlyList<HookTarget> All { get; } = Array.AsReadOnly(new[]
        {
            WM("Picker", "TeleportWorld", "Interact", "Humanoid,Boolean,Boolean", "prefix: Use keeps the vanilla tag input; the mode cycle key cycles the access mode; the tag edit key on a hub edits its name (A5, U1, U6, T12b)"),
            WM("Picker", "TeleportWorld", "GetHoverText", "", "postfix: hover lines: hub name, walk in to choose, access mode, hub rename (T12b)"),
            WM("Picker", "TeleportWorld", "Awake", "", "postfix: register the owner-side RPC for the fields the server decided (T12b)"),
            M("Picker", "TeleportWorld", "GetText", "", "portal tag"),
            WM("Picker", "TeleportWorld", "SetText", "String", "prefix: a tag change on a Private or Admin portal goes to the server, owner or admin only (runtime decision 7)"),
            WM("Travel", "TeleportWorld", "Teleport", "Player", "prefix: walking into any portal opens the map picker (U1, follow-up); the map's Return To Hub button goes to the player's home hub (A4, B2, follow-ups 2 and 3)"),
            WM("Travel", "TeleportWorld", "HaveTarget", "", "postfix: connected glow for every portal, whose target the map picks (T12)"),
            WM("Travel", "TeleportWorld", "TargetFound", "", "postfix: every portal shows its swirl, as Wayfare (T12)"),
            WF("Travel", "TeleportWorld", "m_nview", "ZNetView", "the portal's own ZDO: id, tag, mode, owner, hub, facing and tag pair (T12)"),
            WF("Travel", "TeleportWorld", "m_exitDistance", "Single", "exit point in front of the target portal (T12)"),
            WF("Travel", "TeleportWorld", "m_allowAllItems", "Boolean", "carry rule: the portal walked into decides (U4, T12)"),
            WM("Travel", "Player", "TeleportTo", "Vector3,Quaternion,Boolean", "the client teleports to the clicked portal on the game's own path with fade and loading (C3, U3, T12); a true answer starts a quick jump (T14)"),
            WM("Travel", "Inventory", "IsTeleportable", "Boolean", "carry rule (C1): the Boolean is allowAllItems (ILSpy read); true leaves only the game's hard block (T12)"),
            WM("Travel", "Inventory", "CountItems", "String,Int32,Boolean", "fuel count by the item's shared name (T12)"),
            WM("Travel", "Inventory", "RemoveItem", "String,Int32,Int32,Boolean", "fuel consumption after the game accepted the teleport (T12)"),
            WM("List", "ZDOMan", "GetPortalList", "", "server portal list (D1, T11)"),
            WM("List", "ZDOMan", "AddIfPortal", "ZDO,Int32", "Wayfare discovery: move widened prefabs into the portal list (T11)"),
            WF("List", "ZDOMan", "m_objectsByID", "Dictionary`2<ZDOID,ZDO>", "Wayfare discovery: reclassify loaded ZDOs (T11)"),
            WM("List", "Game", "get_PortalPrefabHash", "", "Wayfare discovery: widen the List<int> property to every TeleportWorld prefab (A6, T11)"),
            WM("List", "ZDOMan", "LoadChunks", "String,FileHelpers/FileSource,Version/World", "postfix: reclassify widened portals of a chunked world save (T11)"),
            WM("List", "ZDOMan", "Load", "BinaryReader,Version/World", "postfix: reclassify widened portals of a single-file world save (T11)"),
            M("List", "Game", "ConnectPortals", "", "vanilla tag pairing, read only, never patched"),
            WM("Map", "Minimap", "IsExplored", "Vector3", "Only Explored Portals (A7, the picker list and the map pane, T13b)"),
            WM("Map", "Minimap", "WorldToMapPoint", "Vector3,Single&,Single&", "map pane pin and player arrow positions (TH06, T13b)"),
            WF("Map", "Minimap", "m_mapTexture", "Texture2D", "map pane image, the game's own map at runtime (TH06, T13b)"),
            WF("Map", "Minimap", "m_fogTexture", "Texture2D", "map pane fog of war over unexplored land (TH06, T13b)"),
            WF("Map", "Minimap", "m_largeMarker", "RectTransform", "map pane player arrow sprite, the game's own (TH06, T13b)"),
            WF("Map", "Piece", "m_icon", "Sprite", "map pane portal pin sprite, the portal piece's own icon (TH06, T13b)"),
            WF("Map", "Minimap", "m_pinRootLarge", "RectTransform", "ordinary map pins: Threshold's layer sits beside the game's large-map pin root (#13)"),
            WF("Map", "Minimap", "m_mapImageLarge", "RawImage", "ordinary map pins: the large map's view rectangle and size (#13)"),
            WF("Map", "Minimap", "m_mode", "Minimap/MapMode", "ordinary map pins: drawn only while the large map is open (#13)"),
            WF("Map", "Minimap", "m_icons", "List`1<Minimap/SpriteData>", "ordinary map pins: the game's own map portal pin glyph (PinType.Icon4), as Wayfare's icons (#13)"),
            M("Travel", "TeleportWorldTrigger", "OnTriggerEnter", "Collider", "the walk-in trigger; its collider tells when the player stepped out and the picker closes (#114)"),
            WF("Favorites", "Player", "m_customData", "Dictionary`2<String,String>", "favorites and the per-world home hub in character data"),
            WM("Favorites", "ZNet", "GetWorldUID", "", "world id of the home hub's character data key (Threshold follow-up 2)"),
            M("Hubs", "Piece", "GetCreator", "", "Hub Editing CreatorOrAdmin (B4)"),
            WM("Access", "Piece", "SetCreator", "Int64,PlatformUserID", "prefix and postfix: a portal placed now gets the Default Access Mode (runtime decision 2)"),
            WM("Jumps", "Player", "UpdateTeleport", "Single", "prefix: Quick Portals hurries the timer of a jump Threshold started (T14, U5)"),
            WF("Jumps", "Player", "m_teleporting", "Boolean", "quick jump: the local player is jumping (T14)"),
            WF("Jumps", "Player", "m_distantTeleport", "Boolean", "quick jump: a long jump, the only kind hurried (T14)"),
            WF("Jumps", "Player", "m_teleportTimer", "Single", "quick jump: the game's jump timer, pushed by JumpRules.Step (T14)"),
            WF("Jumps", "Player", "m_teleportFromPos", "Vector3", "quick jump: start of the jump for its map distance (T14)"),
            WF("Jumps", "Player", "m_teleportTargetPos", "Vector3", "quick jump: target of the jump and of the area checks (T14)"),
            WM("Jumps", "Hud", "UpdateBlackScreen", "Player,Single", "prefix: Screen Only When Loading keeps a jump into a loaded area clear (T14)"),
            WF("Jumps", "Hud", "m_loadingScreen", "CanvasGroup", "quick jump screen fade (T14)"),
            WF("Jumps", "Hud", "m_haveSetupLoadScreen", "Boolean", "quick jump screen: the game's own fade-out state (T14)"),
            WM("Jumps", "ZNetScene", "IsAreaReady", "Vector3", "quick jump: the target area is loaded already (T14)"),
            WM("Jumps", "ZoneSystem", "Update", "", "postfix: Quick Area Loading loads the land faster during a Threshold jump (T14)"),
            WM("Jumps", "ZoneSystem", "CreateLocalZones", "Vector3", "Quick Area Loading: the game's own zone step, called again within a budget (T14)"),
            WM("Jumps", "ZoneSystem", "IsActiveAreaLoaded", "", "Quick Area Loading: stop once the area is in (T14)"),
            WM("Jumps", "ZNetScene", "CreateDestroyObjects", "", "postfix: Quick Area Loading creates more listed objects during a Threshold jump (T14)"),
            WM("Jumps", "ZNetScene", "CreateObject", "ZDO", "Quick Area Loading: the game's own object creation (T14)"),
            WM("Jumps", "ZNetScene", "ZDOCompare", "ZDO,ZDO", "Quick Area Loading: the game's own creation order (T14)"),
            WF("Jumps", "ZNetScene", "m_tempCurrentObjects", "List`1<ZDO>", "Quick Area Loading: the objects the game has just listed (T14)"),
            WM("SeaGates", "ZRoutedRpc", ".ctor", "Boolean", "postfix: the sea gate list, grant and crew jump calls for every session (T14)"),
            WM("SeaGates", "ZNetScene", "Awake", "", "postfix: the Sea Gate Pillar prefab joins the scene (T14, U8)"),
            WM("SeaGates", "ObjectDB", "Awake", "", "postfix: the pillar's recipe and its place in the hammer (T14)"),
            WM("SeaGates", "Player", "UpdatePlacementGhost", "Boolean", "postfix: a pillar ghost deeper than 2 m under water is invalid (T14)"),
            WM("SeaGates", "Player", "TryPlacePiece", "Piece", "postfix: say why a pillar was refused (T14)"),
            WM("SeaGates", "ZNetScene", "Destroy", "GameObject", "prefix: a pillar destroyed for real unpairs its partner (T14)"),
            WM("SeaGates", "Game", "OnDestroy", "", "postfix: a world unload drops grants and gate surfaces (T14)"),
            WM("SeaGates", "Ship", "Awake", "", "postfix: the ship's landed and pick calls, the ship list (T14)"),
            WM("SeaGates", "Ship", "CustomFixedUpdate", "Single", "prefix: a ship stopped by a gate stays still (T14)"),
            WM("SeaGates", "WearNTear", "ApplyDamage", "Single,HitData", "prefix: a stopped or protected ship takes no damage (T14)"),
            WM("SeaGates", "Player", "OnDeath", "", "prefix: death ends a crew hold (T14)"),
            WM("SeaGates", "Player", "OnDestroy", "", "prefix: logout ends a crew hold (T14)"),
            WM("SeaGates", "PlayerProfile", "SaveLogoutPoint", "", "postfix: a player held mid-jump logs out beside the destination gate (T14)"),
            WM("SeaGates", "Character", "RPC_Damage", "Int64,HitData", "prefix: no damage for a few seconds after a sea gate landing (T14)"),
            WM("SeaGates", "Character", "ApplyDamage", "HitData,Boolean,Boolean,HitData/DamageModifier", "prefix: no damage ticks after a sea gate landing (T14)"),
            WM("Travel", "Inventory", "GetAllItems", "", "Teleport Listed Items: the restricted items carried, by prefab name (T17)"),
            WM("Tames", "Character", "GetAllCharacters", "", "Bring Tames: creatures near the traveler (T18)"),
            WM("Tames", "MonsterAI", "GetFollowTarget", "", "Bring Tames: the game's follow command targets the traveler (T18)"),
            WM("Tames", "ZDOMan", "GetSessionID", "", "Bring Tames: this client takes ownership of the creatures it moves (T18)"),
            WM("Tames", "ZDO", "SetOwner", "Int64", "Bring Tames: ownership before the move (T18)"),
            WM("Tames", "ZDO", "SetPosition", "Vector3", "Bring Tames: the creature's new place; an unloaded one is rebuilt there by streaming (T18)"),
            WM("Tames", "ZNetScene", "FindInstance", "ZDO", "Bring Tames: a creature still loaded after a short jump is moved directly (T18)"),
            WM("Arrival", "MonsterAI", "UpdateTarget", "Humanoid,Single,Boolean&,Boolean&", "postfix: Arrival Protection Seconds, creatures drop a player who has just come through a portal"),
            WF("Arrival", "MonsterAI", "m_targetCreature", "Character", "Arrival Protection Seconds: the creature's current target"),
            WF("Arrival", "Character", "m_nview", "ZNetView", "Arrival Protection Seconds: the player's ZDO carries the protection end time"),
            WM("Map", "Minimap", "InTextInput", "", "Portal Pins Key: no toggle while a map pin name is typed"),
            WF("Carts", "Vagon", "m_instances", "List`1<Vagon>", "Bring Carts: the loaded carts, to find the one the traveler pulls"),
            WM("Carts", "Vagon", "IsAttached", "Character", "Bring Carts: the cart is pulled by the traveler"),
            WM("Carts", "Vagon", "Detach", "", "Bring Carts: the cart is let go before it is moved"),
            WF("Carts", "Vagon", "m_container", "Container", "Bring Carts: the cart's box and load"),
            WF("Carts", "Vagon", "m_nview", "ZNetView", "Bring Carts: the cart's ZDO, which holds its load and is moved whole"),
            WM("Carts", "Vagon", "RPC_RequestOwn", "Int64", "prefix: Bring Carts, nobody takes over a cart while it travels"),
            WM("Carts", "Container", "GetInventory", "", "Bring Carts: the cart's load, checked with the carry rule"),
            WM("Carts", "Container", "IsInUse", "", "Bring Carts: a cart whose box is open stays"),
            WM("Carts", "Container", "RPC_RequestOpen", "Int64,Int64", "prefix: Bring Carts, nobody opens a cart's box while it travels"),
            WM("Carts", "ZDO", "SetRotation", "Quaternion", "Bring Carts: the cart faces the destination's way"),
            WM("Carts", "ZDO", "HasOwner", "", "Bring Carts: a cart another player took over is left alone"),
        });
    }
}
