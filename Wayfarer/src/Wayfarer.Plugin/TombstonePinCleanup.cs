// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using HarmonyLib;
using Splatform;
using UnityEngine;
using Wayfarer.Core;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace Wayfarer {
 internal static class TombstonePinCleanup {
  internal sealed class Retrieval {
   internal TombStone Tomb = null!;
   internal Container Container = null!;
   internal Inventory Inventory = null!;
   internal Player Player = null!;
   internal Game Game = null!;
   internal Minimap Map = null!;
   internal ZDO Zdo = null!;
   internal long World, PlayerId, Generation, Ticks;
   internal Vector3 Original;
   internal int Day;
   internal bool PinMetadata;
  }
  static readonly AccessTools.FieldRef<TombStone,ZNetView> tombView = AccessTools.FieldRefAccess<TombStone,ZNetView>("m_nview");
  static readonly AccessTools.FieldRef<InventoryGui,Container> currentContainer = AccessTools.FieldRefAccess<InventoryGui,Container>("m_currentContainer");
  static readonly AccessTools.FieldRef<InventoryGui,Inventory> dragInventory = AccessTools.FieldRefAccess<InventoryGui,Inventory>("m_dragInventory");
  static readonly AccessTools.FieldRef<InventoryGui,GameObject> dragObject = AccessTools.FieldRefAccess<InventoryGui,GameObject>("m_dragGo");
  static readonly AccessTools.FieldRef<Minimap,List<Minimap.PinData>> mapPins = AccessTools.FieldRefAccess<Minimap,List<Minimap.PinData>>("m_pins");
  static readonly System.Reflection.MethodInfo? getDay = AccessTools.Method(typeof(EnvMan),"GetDay",new[]{typeof(double)});
  static int removed;
  static string lastResult = "idle";
  static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
  static bool Enabled => Plugin.Instance?.Options?.Enabled.Value == true;

  static Retrieval? Capture(TombStone tomb, bool requireItems) {
   if (!tomb || !Player.m_localPlayer || !Game.instance
       || !ZNet.instance || ZNet.instance.GetWorld() == null || ZNet.instance.IsDedicated()) return null;
   var player = Player.m_localPlayer; var game = Game.instance; var profile = game.GetPlayerProfile();
   var playerView = player.GetComponent<ZNetView>(); var view = tombView(tomb); var container = tomb.GetComponent<Container>();
   long world = ZNet.instance.GetWorldUID(), id = player.GetPlayerID();
   if (player.IsDead() || id == 0 || profile == null || profile.GetPlayerID() != id || world == 0
       || !CruciblePlugin.Session.AuthorityReady || CruciblePlugin.Session.WorldId != world
       || !playerView || !playerView.IsValid() || !playerView.IsOwner()
       || !view || !view.IsValid() || !view.IsOwner() || !container) return null;
   var zdo = view.GetZDO(); var inventory = container.GetInventory();
   if (zdo == null || zdo.GetLong(ZDOVars.s_owner,0) != id || inventory == null || requireItems && inventory.NrOfItems() == 0) return null;
   long ticks = zdo.GetLong(ZDOVars.s_timeOfDeath,0);
   Vector3 original=default;int day=-1;bool pinMetadata=false;
   if (EnvMan.instance && ticks > 0 && ticks <= DateTime.MaxValue.Ticks
       && zdo.GetVec3(ZDOVars.s_spawnPoint,out original) && Finite(original.x) && Finite(original.y) && Finite(original.z) && getDay != null)
    pinMetadata=getDay.Invoke(EnvMan.instance,new object[]{ticks / (double)TimeSpan.TicksPerSecond}) is int found && (day=found)>=0;
   return new Retrieval { Tomb=tomb, Container=container, Inventory=inventory, Player=player, Game=game, Map=Minimap.instance,
    Zdo=zdo, World=world, PlayerId=id, Generation=CruciblePlugin.Session.Generation, Ticks=ticks, Original=original, Day=day, PinMetadata=pinMetadata };
  }

  internal static Retrieval? FromTakeAll(TombStone tomb) {
   try { return Capture(tomb,false); }
   catch (Exception error) { Plugin.Instance?.Error(error); return null; }
  }
  internal static Retrieval? FromGui(InventoryGui gui, InventoryGrid? grid = null, ItemDrop.ItemData? item = null, InventoryGrid.Modifier? modifier = null) {
   try {
    var container = currentContainer(gui);
    if (!container) return null;
    var tomb = container.GetComponent<TombStone>();
    if (!tomb) return null;
    var capture = Capture(tomb,true);
    if (capture == null || !ReferenceEquals(capture.Container,container)) return null;
    if (grid == null) return capture;
    var destination = grid.GetInventory();
    // Native dragging precedes the modifier switch; only grave-to-player transfers qualify.
    if (dragObject(gui)) return ReferenceEquals(dragInventory(gui),capture.Inventory)
      && ReferenceEquals(destination,capture.Player.GetInventory()) ? capture : null;
    return modifier == InventoryGrid.Modifier.Move && item != null && ReferenceEquals(destination,capture.Inventory) ? capture : null;
   } catch (Exception error) { Plugin.Instance?.Error(error); return null; }
  }
  internal static void Complete(Retrieval? capture, bool original) {
   if (capture == null || !original) return;
   try {
    var now = Capture(capture.Tomb,false);
    if (now == null || !ReferenceEquals(now.Player,capture.Player) || !ReferenceEquals(now.Game,capture.Game)
        || !ReferenceEquals(now.Container,capture.Container)
        || !ReferenceEquals(now.Inventory,capture.Inventory) || !ReferenceEquals(now.Zdo,capture.Zdo)
        || now.World != capture.World || now.PlayerId != capture.PlayerId || now.Generation != capture.Generation
        || now.Ticks != capture.Ticks) { lastResult="scope-changed"; return; }
    if (now.Inventory.NrOfItems() != 0) { lastResult="items-remain"; return; }
    NotifyFoodRetrieved(now);
    if (!Enabled || !now.Map || !ReferenceEquals(now.Map,capture.Map) || !now.PinMetadata || !capture.PinMetadata
        || now.Day!=capture.Day || !now.Original.Equals(capture.Original)) { lastResult="pin-unavailable";return; }
    var pins = mapPins(now.Map).ToArray();
    var candidates = new List<DeathPinCandidate>(pins.Length);
    foreach (var pin in pins) {
     bool foreignAuthor = pin.m_author.IsValid && (PlatformManager.DistributionPlatform == null
       || pin.m_author != PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
     candidates.Add(new DeathPinCandidate(pin.m_pos.x,pin.m_pos.z,pin.m_name,pin.m_save,
       pin.m_type == Minimap.PinType.Death,pin.m_ownerID,foreignAuthor));
    }
    int index = DeathPinCleanup.FindUnique(candidates,now.Original.x,now.Original.z,now.Day,now.PlayerId);
    if (index < 0) { lastResult="no-unique-pin"; return; }
    var pinToRemove = pins[index];
    if (!mapPins(now.Map).Contains(pinToRemove)) { lastResult="pin-changed"; return; }
    now.Map.RemovePin(pinToRemove); removed++; lastResult="removed";
   } catch (Exception error) { lastResult="error"; Plugin.Instance?.Error(error); }
  }
  static void NotifyFoodRetrieved(Retrieval now) {
   const string capability="meadworks.larder.food.retrieved";
   try {
    var registry=CruciblePlugin.Services.Capabilities;var providers=registry.Providers(capability);
    if(providers.Count==1 && providers[0].Owner=="com.meadworks.larder"
       && registry.TryResolve<Action<long,long,string>>(capability,new System.Version(1,0),out var notify))
     notify(now.World,now.PlayerId,now.Zdo.m_uid.ToString());
   } catch(Exception error) { Plugin.Instance?.Error(error); }
  }
  internal static object Status() => new { enabled=Enabled, removed, lastResult };
 }
 [HarmonyPatch(typeof(TombStone),"OnTakeAllSuccess")]
 internal static class TombstonePinTakeAllPatch {
  [HarmonyPrefix] static void Before(TombStone __instance,out TombstonePinCleanup.Retrieval? __state) => __state=TombstonePinCleanup.FromTakeAll(__instance);
  [HarmonyPostfix] static void After(TombstonePinCleanup.Retrieval? __state,bool __runOriginal) => TombstonePinCleanup.Complete(__state,__runOriginal);
 }
 [HarmonyPatch(typeof(InventoryGui),"OnTakeAll")]
 internal static class TombstonePinGuiTakeAllPatch {
  [HarmonyPrefix] static void Before(InventoryGui __instance,out TombstonePinCleanup.Retrieval? __state) => __state=TombstonePinCleanup.FromGui(__instance);
  [HarmonyPostfix] static void After(TombstonePinCleanup.Retrieval? __state,bool __runOriginal) => TombstonePinCleanup.Complete(__state,__runOriginal);
 }
 [HarmonyPatch(typeof(InventoryGui),"OnSelectedItem")]
 internal static class TombstonePinGuiTransferPatch {
  [HarmonyPrefix] static void Before(InventoryGui __instance,InventoryGrid grid,ItemDrop.ItemData item,InventoryGrid.Modifier mod,out TombstonePinCleanup.Retrieval? __state)
   => __state=TombstonePinCleanup.FromGui(__instance,grid,item,mod);
  [HarmonyPostfix] static void After(TombstonePinCleanup.Retrieval? __state,bool __runOriginal) => TombstonePinCleanup.Complete(__state,__runOriginal);
 }
}

