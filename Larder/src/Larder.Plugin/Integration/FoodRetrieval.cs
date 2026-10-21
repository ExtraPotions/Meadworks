// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Larder.Runtime
{
    internal static class FoodRetrieval
    {
        internal sealed class Capture
        {
            internal TombStone Tomb;
            internal Container Container;
            internal Inventory Inventory;
            internal Player Player;
            internal Game Game;
            internal ZDO Zdo;
            internal long World, PlayerId, Generation;
        }
        static readonly FieldInfo currentContainer = AccessTools.Field(typeof(InventoryGui),"m_currentContainer");
        static readonly FieldInfo dragInventory = AccessTools.Field(typeof(InventoryGui),"m_dragInventory");
        static readonly FieldInfo dragObject = AccessTools.Field(typeof(InventoryGui),"m_dragGo");
        internal static string LastError { get; private set; }

        internal static Capture Before(Container container)
        {
            try
            {
                var tomb=container ? container.GetComponent<TombStone>() : null;
                var player=Player.m_localPlayer;var game=Game.instance;var net=ZNet.instance;
                if(!Plugin.Ready || !Plugin.AllowsUi || !tomb || !player || player.IsDead() || !game || !net || net.IsDedicated() || net.GetWorld()==null)return null;
                long world=net.GetWorldUID(),id=player.GetPlayerID();var profile=game.GetPlayerProfile();
                var pv=player.GetComponent<ZNetView>();var tv=tomb.GetComponent<ZNetView>();var inventory=container.GetInventory();
                if(world==0 || id==0 || profile==null || profile.GetPlayerID()!=id || !CruciblePlugin.Session.AuthorityReady
                    || CruciblePlugin.Session.WorldId!=world || !pv || !pv.IsValid() || !pv.IsOwner()
                    || !tv || !tv.IsValid() || tv.GetZDO()==null || tv.GetZDO().GetLong(ZDOVars.s_owner,0)!=id
                    || inventory==null || inventory.NrOfItems()==0)return null;
                return new Capture{Tomb=tomb,Container=container,Inventory=inventory,Player=player,Game=game,Zdo=tv.GetZDO(),World=world,PlayerId=id,Generation=CruciblePlugin.Session.Generation};
            }
            catch(Exception error){LastError=error.GetType().Name+": "+error.Message;return null;}
        }

        internal static Capture BeforeTransfer(InventoryGui gui,InventoryGrid grid,ItemDrop.ItemData item,InventoryGrid.Modifier modifier)
        {
            try
            {
                var capture=Before((Container)currentContainer.GetValue(gui));if(capture==null || grid==null)return null;
                var destination=grid.GetInventory();
                if((GameObject)dragObject.GetValue(gui))return ReferenceEquals(dragInventory.GetValue(gui),capture.Inventory)
                    && ReferenceEquals(destination,capture.Player.GetInventory()) ? capture : null;
                return modifier==InventoryGrid.Modifier.Move && item!=null && ReferenceEquals(destination,capture.Inventory) ? capture : null;
            }
            catch(Exception error){LastError=error.GetType().Name+": "+error.Message;return null;}
        }

        internal static void Complete(Capture capture,bool original)
        {
            if(capture==null || !original)return;
            try
            {
                var player=Player.m_localPlayer;var net=ZNet.instance;
                if(!Plugin.Ready || !Plugin.AllowsUi || !capture.Tomb || !capture.Container || player!=capture.Player || !player || player.IsDead()
                    || Game.instance!=capture.Game || !net || net.GetWorld()==null || net.GetWorldUID()!=capture.World
                    || player.GetPlayerID()!=capture.PlayerId || Game.instance.GetPlayerProfile()?.GetPlayerID()!=capture.PlayerId
                    || !CruciblePlugin.Session.AuthorityReady || CruciblePlugin.Session.WorldId!=capture.World || CruciblePlugin.Session.Generation!=capture.Generation)return;
                var pv=player.GetComponent<ZNetView>();var view=capture.Tomb.GetComponent<ZNetView>();
                if(!pv || !pv.IsValid() || !pv.IsOwner() || !view || !view.IsValid() || !view.IsOwner()
                    || !ReferenceEquals(view.GetZDO(),capture.Zdo) || capture.Zdo.GetLong(ZDOVars.s_owner,0)!=capture.PlayerId
                    || !ReferenceEquals(capture.Container.GetInventory(),capture.Inventory) || capture.Inventory.NrOfItems()!=0)return;
                FoodTopUp.Retrieved(capture.World,capture.PlayerId,capture.Zdo.m_uid.ToString());
            }
            catch(Exception error){LastError=error.GetType().Name+": "+error.Message;}
        }

        [HarmonyPatch(typeof(Container),"RPC_TakeAllResponse")]
        internal static class TakeAllPatch
        {
            [HarmonyPrefix] internal static void Before(Container __instance,bool granted,out Capture __state)
                => __state=granted ? FoodRetrieval.Before(__instance) : null;
            [HarmonyPostfix] internal static void After(Capture __state,bool __runOriginal) => Complete(__state,__runOriginal);
        }
        [HarmonyPatch(typeof(InventoryGui),"OnSelectedItem")]
        internal static class TransferPatch
        {
            [HarmonyPrefix] internal static void Before(InventoryGui __instance,InventoryGrid grid,ItemDrop.ItemData item,InventoryGrid.Modifier mod,out Capture __state)
                => __state=BeforeTransfer(__instance,grid,item,mod);
            [HarmonyPostfix] internal static void After(Capture __state,bool __runOriginal) => Complete(__state,__runOriginal);
        }
    }
}
