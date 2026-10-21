// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the travel patch (T12). Walking into any portal opens the travel map (U1), the player's client holds the clicked destination and teleports on the game's own path (U3), the entry
// portal decides for ores by default (U4), fuel only on trips Threshold routes and waived by the Ignore Teleport Restrictions cheat
// (U10), Return To Hub only to the player's chosen home hub. Behavior as Wayfare's portal trigger,
// open-portal look and teleport completion (GPL-3.0-or-later, the reference) and PotalMap's walk-in map (behavior only).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Picker;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Routing;
using ExtraPotions.Threshold.Runtime.Map;
using ExtraPotions.Threshold.Runtime.Portals;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.Travel
{
    /// <summary>
    /// One travel session: the portal walked into while the picker window is open. The window (T13b) is the host:
    /// <see cref="Host"/> opens it for a source portal and returns false when it cannot (headless, no kit), in which case
    /// the game's own tag-paired teleport runs. A click or the Return To Hub button calls <see cref="TravelTo"/> or
    /// <see cref="ReturnToHub"/>; closing the window ends the session with no trip.
    /// </summary>
    public static class ThresholdTravel
    {
        static readonly AccessTools.FieldRef<TeleportWorld, ZNetView> View = AccessTools.FieldRefAccess<TeleportWorld, ZNetView>("m_nview");
        static readonly int ModeHash = PortalFields.ModeKey.GetStableHashCode();
        static readonly int OwnerHash = PortalFields.OwnerKey.GetStableHashCode();
        static readonly int HubHash = PortalFields.HubKey.GetStableHashCode();

        /// <summary>Opens the picker for the portal walked into; false when no picker can be shown.</summary>
        public static Func<TeleportWorld, bool>? Host { get; set; }

        /// <summary>Whether a picker could be shown now (cheap; checked inside the game's trigger, before the open is deferred).</summary>
        public static Func<bool>? CanHost { get; set; }

        static readonly DeferredOpen<TeleportWorld> walkIn = new DeferredOpen<TeleportWorld>();
        static bool vanillaJump;
        static TeleportMode sessionMode;

        /// <summary>Asked to close the picker (a trip started, or the session ended).</summary>
        public static Action? CloseHost { get; set; }

        /// <summary>The portal walked into, or null outside a session.</summary>
        public static TeleportWorld? Source { get; private set; }

        public static bool InSession => Source != null && SourceZdo(Source) != null;

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(TeleportWorld), nameof(TeleportWorld.Teleport), new[] { typeof(Player) }),
                prefix: new HarmonyMethod(typeof(ThresholdTravel), nameof(BeforeTeleport)));
            var open = new HarmonyMethod(typeof(ThresholdTravel), nameof(LooksOpen));
            harmony.Patch(AccessTools.Method(typeof(TeleportWorld), "HaveTarget"), postfix: open);
            harmony.Patch(AccessTools.Method(typeof(TeleportWorld), "TargetFound"), postfix: open);
            ThresholdArrival.Patch(harmony);
            ThresholdCarts.Patch(harmony);
        }

        /// <summary>Walking in (the trigger of the local player): the travel map instead of the tag-paired jump.</summary>
        static bool BeforeTeleport(TeleportWorld __instance, Player player)
        {
            try
            {
                if (vanillaJump || player == null || player != Player.m_localPlayer || player.IsTeleporting() || !ThresholdModules.PortalRoutingActive()) return true;
                var entry = EntryOf(__instance);
                if (entry == null) return true;
                if (!PortalModeRules.CanEnter(ThresholdModules.Mode, entry))
                {
                    player.Message(MessageHud.MessageType.Center, ThresholdText.Text(ThresholdTextKeys.PickerUnnamed));
                    return false;
                }
                var route = RouteTable.Decide(Facts(PortalAction.Enter, entry, null));
                if (route.Kind != RouteKind.OpenPicker) return true;
                if (Source == __instance && InSession) return false; // already open for this portal
                if (Host == null || (CanHost != null && !CanHost())) return ThresholdModules.Mode != TeleportMode.TargetTeleport;
                // Decided here, inside the game's trigger; the picker is built and opened on the next frame (OpenPending).
                sessionMode = ThresholdModules.Mode;
                Source = __instance;
                ThresholdPortals.RefreshNow();
                walkIn.Request(__instance);
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("Threshold: walk-in failed, the game's portal runs: " + e);
                Source = null;
                return true;
            }
        }

        /// <summary>From the plugin's Update: opens the picker for the portal walked into last frame. If it cannot open,
        /// the game's own jump runs as it would have in the trigger.</summary>
        public static void OpenPending()
        {
            if (Source != null && sessionMode != ThresholdModules.Mode)
            {
                walkIn.Clear();Source = null;CloseHost?.Invoke();return;
            }
            var portal = walkIn.Take();
            if (portal == null || Source != portal) return;
            var player = Player.m_localPlayer;
            bool opened = false;
            try { opened = player != null && Host != null && Host(portal); }
            catch (Exception e) { Plugin.Log?.Error("Threshold: walk-in failed, the game's portal runs: " + e); }
            if (opened) return;
            Source = null;
            if (player == null || portal == null || ThresholdModules.Mode == TeleportMode.TargetTeleport) return;
            vanillaJump = true;
            try { portal.Teleport(player); }
            finally { vanillaJump = false; }
        }

        /// <summary>Every portal looks open while Threshold routes: any portal reaches any other, tagged or not.</summary>
        static void LooksOpen(TeleportWorld __instance, ref bool __result)
        {
            if (__instance == null || !ThresholdModules.PortalRoutingActive() || SourceZdo(__instance) == null) return;
            __result = ThresholdModules.Mode != TeleportMode.TargetTeleport || EntryOf(__instance)?.IsTagged == true;
        }

        /// <summary>The window closed without a trip.</summary>
        public static void EndSession() => Source = null;

        /// <summary>The portal walked into as a list entry (read from its own ZDO, which a client holds while near it).</summary>
        public static PortalEntry? SourceEntry => Source != null ? EntryOf(Source) : null;

        /// <summary>The hub of the map's Return To Hub button for this session, or null (no button).</summary>
        public static PortalEntry? ReturnHub()
        {
            var entry = SourceEntry;
            if (entry == null || !ThresholdModules.PortalRoutingActive()) return null;
            var route = RouteTable.Decide(Facts(PortalAction.ReturnToHub, entry, null));
            return route.Kind == RouteKind.TravelToHub ? route.Target : null;
        }

        /// <summary>A click on a destination (U1): the client's trip check, then the game's teleport (U3).</summary>
        public static bool TravelTo(PortalEntry destination)
        {
            var entry = SourceEntry;
            if (entry == null || destination == null) return false;
            destination = TravelRules.CurrentTarget(destination, ThresholdPortals.Current)!;
            if (destination == null || !PortalModeRules.CanEnter(ThresholdModules.Mode, entry)
                || ThresholdModules.Mode == TeleportMode.TargetTeleport && !destination.IsTagged) return false;
            return Go(RouteTable.Decide(Facts(PortalAction.Pick, entry, destination)));
        }

        /// <summary>The map's Return To Hub button: to the player's home hub only.</summary>
        public static bool ReturnToHub()
        {
            var entry = SourceEntry;
            if (entry == null) return false;
            return Go(RouteTable.Decide(Facts(PortalAction.ReturnToHub, entry, null)));
        }

        static bool Go(Route route)
        {
            var player = Player.m_localPlayer;
            var source = Source;
            var settings = Plugin.Settings;
            if (player == null || source == null || settings == null || !ThresholdModules.PortalRoutingActive()) return false;
            var sourceEntry = EntryOf(source);
            if (sourceEntry == null || !PortalModeRules.CanEnter(ThresholdModules.Mode, sourceEntry)) return false;
            var inventory = player.GetInventory();
            var zones = ZoneSystem.instance;
            bool noPortals = zones != null && zones.GetGlobalKey(GlobalKeys.NoPortals);
            float bosses = 0f;
            bool bossBlocked = zones != null && TravelRules.BossBlocked(zones.GetGlobalKey(GlobalKeys.NoBossPortals),
                RandEventSystem.instance != null && RandEventSystem.instance.GetBossEvent() != null,
                zones.GetGlobalKey(GlobalKeys.activeBosses, out bosses) ? bosses : 0f);
            bool cheat = settings.IgnoreTeleportRestrictions;
            bool fuelOn = settings.Get(ThresholdSettings.Fuel.FuelEnabled);
            string fuelPrefab = settings.Get(ThresholdSettings.Fuel.FuelItem);
            string fuelName = FuelName(fuelPrefab);
            int carried = fuelOn && fuelName.Length > 0 ? inventory.CountItems(fuelName) : 0;
            var plan = TravelRules.Decide(new TravelFacts(route, noPortals, bossBlocked, !inventory.IsTeleportable(true), Teleportable(inventory, settings),
                source.m_allowAllItems, cheat, fuelOn, settings.Get(ThresholdSettings.Fuel.FuelAmount), carried,
                settings.Get(ThresholdSettings.General.OreTransport)));
            if (!plan.Go)
            {
                Refuse(player, plan, fuelName.Length > 0 ? fuelName : fuelPrefab);
                return false;
            }
            var target = plan.Target!;
            if (ThresholdModules.Mode == TeleportMode.TargetTeleport && !target.IsTagged) return false;
            var exit = TravelRules.ExitPoint(target.Position, target.Yaw, source.m_exitDistance);
            var cart = ThresholdCarts.Pulled(player);
            var cartOutcome = ThresholdCarts.Decide(cart, settings, source.m_allowAllItems, target.AllowsAllItems, load => Teleportable(load, settings));
            if (cartOutcome == CartOutcome.Refuse)
            {
                player.Message(MessageHud.MessageType.Center, ThresholdCarts.Message(cartOutcome));
                return false;
            }
            var tames = ThresholdTames.Choose(player, settings);
            if (!player.TeleportTo(new Vector3(exit.X, exit.Y, exit.Z), Quaternion.Euler(0f, target.Yaw, 0f), true)) return false;
            ThresholdJumps.Started(player);
            ThresholdArrival.Begin(player);
            ThresholdTames.Begin(tames, exit, target.Yaw);
            if (cartOutcome == CartOutcome.Bring) ThresholdCarts.Begin(cart!, exit, target.Yaw);
            else if (cartOutcome == CartOutcome.StayBusy) player.Message(MessageHud.MessageType.Center, ThresholdCarts.Message(cartOutcome));
            if (plan.Fuel.Outcome == FuelOutcome.Consume) inventory.RemoveItem(fuelName, plan.Fuel.Amount);
            Game.instance?.IncrementPlayerStat(PlayerStatType.PortalsUsed);
            Plugin.Log?.Verbose("Threshold: trip to " + target);
            Source = null;
            CloseHost?.Invoke();
            return true;
        }

        /// <summary>The game's inventory check with only the world's rules, widened by Teleport Listed Items (T17) while
        /// that cheat is in effect and TeleportEverything is absent. Restricted items are named by their prefab.</summary>
        static bool Teleportable(Inventory inventory, ThresholdConfig settings)
        {
            bool game = inventory.IsTeleportable(false);
            if (game || !ThresholdModules.ExtrasActive() || !settings.Get(ThresholdSettings.Cheats.TeleportListedItems)) return game;
            var restricted = inventory.GetAllItems().Where(i => i != null && !i.m_shared.m_teleportable)
                .Select(i => i.m_dropPrefab != null ? i.m_dropPrefab.name : i.m_shared.m_name).ToList();
            string listed = settings.Get(ThresholdSettings.Cheats.ListedItems);
            bool allowed = CarryRules.Teleportable(false, restricted, true, listed);
            Plugin.Log?.Debug("Threshold listed items: " + (allowed ? "carry allowed for " + string.Join(", ", restricted.Distinct())
                : "still blocked by " + string.Join(", ", CarryRules.Unlisted(restricted, listed))));
            return allowed;
        }

        static void Refuse(Player player, TravelPlan plan, string fuelName)
        {
            string key = TravelRules.TextKey(plan.Refusal);
            if (key.Length == 0) return;
            string text = key.StartsWith("$", StringComparison.Ordinal) ? key
                : plan.Refusal == TravelRefusal.Fuel
                    ? ThresholdText.Format(key, plan.Fuel.Amount, global::Localization.instance != null ? global::Localization.instance.Localize(fuelName) : fuelName)
                    : ThresholdText.Text(key);
            player.Message(MessageHud.MessageType.Center, text);
        }

        /// <summary>The item's shared name (what inventories count by) for the Fuel Item prefab name; empty if unknown.</summary>
        static string FuelName(string prefab)
        {
            var db = ObjectDB.instance;
            var item = db != null ? db.GetItemPrefab(prefab) : null;
            var drop = item != null ? item.GetComponent<ItemDrop>() : null;
            return drop != null ? drop.m_itemData.m_shared.m_name : "";
        }

        static RouteFacts Facts(PortalAction action, PortalEntry portal, PortalEntry? destination)
        {
            var settings = Plugin.Settings!;
            var player = Player.m_localPlayer;
            var viewer = new AccessViewer(player != null ? player.GetPlayerID() : 0L, ZNet.instance != null && ZNet.instance.LocalPlayerIsAdminOrHost());
            var zdo = Source != null ? SourceZdo(Source) : null;
            bool pair = zdo != null && zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal) != ZDOID.None;
            return new RouteFacts(action, ThresholdModules.PortalRoutingActive(), portal, pair, settings.Get(ThresholdSettings.Hubs.ReturnToHub), destination,
                ThresholdPortals.Current, viewer, settings.Get(ThresholdSettings.Access.AccessModesEnabled), ThresholdHomeHub.Load());
        }

        /// <summary>A portal as a list entry, from its own ZDO.</summary>
        public static PortalEntry? EntryOf(TeleportWorld portal)
        {
            var zdo = SourceZdo(portal);
            if (zdo == null) return null;
            var at = zdo.GetPosition();
            var position = new MapPoint(at.x, at.y, at.z);
            if (!position.IsFinite) return null;
            var name = Utils.GetPrefabName(portal.gameObject);
            return new PortalEntry(new PortalId(zdo.m_uid.UserID, zdo.m_uid.ID), string.IsNullOrEmpty(name) ? "portal" : name, zdo.GetString(ZDOVars.s_tag),
                position, PortalFields.Mode(zdo.GetInt(ModeHash, PortalFields.NoMode)), zdo.GetLong(OwnerHash, 0L), zdo.GetString(HubHash, ""),
                portal.m_allowAllItems, zdo.GetRotation().eulerAngles.y);
        }

        static ZDO? SourceZdo(TeleportWorld portal)
        {
            if (portal == null) return null;
            var view = View(portal);
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }

        public static void Reset()
        {
            walkIn.Clear();vanillaJump = false;
            Source = null;
            ThresholdTames.Reset();
            ThresholdArrival.Reset();
            ThresholdCarts.Reset();
        }
    }
}
