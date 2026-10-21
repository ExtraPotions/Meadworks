// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Larder.Runtime
{
    // Native item use owns consumption, effects and payment. The pending request owns only its actor/world and a deadline.
    internal static class FoodTopUp
    {
        internal const string RetrievalCapability = "meadworks.larder.food.retrieved";
        static ConfigEntry<bool> enabled;
        internal static ConfigEntry<bool> AutoFromSlots;
        static float nextAuto;
        static Harmony patches;
        static Player actor, lastSpawn;
        static long world, playerId;
        static float deadline, nextCheck;
        static bool pending, eating;
        static readonly HashSet<string> graves = new HashSet<string>(StringComparer.Ordinal);
        static string lastResult = "idle";
        static int attempts, consumed;

        internal static void Initialize(ConfigFile config, ProductRegistration source)
        {
            Shutdown();
            AutoFromSlots = MeadworksConfig.Bind(config,"3 - Eating","Eat From Food Slots Automatically",true,
                "Whenever food in your configured Mantle Food slots can be eaten (a free food slot, or a food low enough that the game lets you eat it again), eat it using the game's normal eating rules. Consumes actual items; does nothing without Mantle Food slots.",ConfigScope.Local);
            enabled = MeadworksConfig.Bind(config,"3 - Eating","Food Slots On Spawn And Retrieval",true,
                "After joining a world, respawning or fully retrieving your own tombstone, eat available food from your configured Mantle Food slots using the game's normal eating rules. Consumes actual items; does nothing without Mantle Food slots.",ConfigScope.Local);
            source.AddCapability<Action<long,long,string>>(RetrievalCapability,new System.Version(1,0),Retrieved);
            patches = new Harmony(Plugin.Guid + ".foodtopup");
            patches.CreateClassProcessor(typeof(SpawnPatch)).Patch();
            patches.CreateClassProcessor(typeof(FoodRetrieval.TakeAllPatch)).Patch();
            patches.CreateClassProcessor(typeof(FoodRetrieval.TransferPatch)).Patch();
        }

        static bool Scope(Player player)
        {
            if (!player || player != Player.m_localPlayer || !ZNet.instance || ZNet.instance.GetWorld()==null) return false;
            long currentWorld=ZNet.instance.GetWorldUID(), currentPlayer=player.GetPlayerID();
            if (currentWorld==0 || currentPlayer==0) return false;
            if (world!=currentWorld || playerId!=currentPlayer)
            {
                pending=false;actor=null;lastSpawn=null;graves.Clear();world=currentWorld;playerId=currentPlayer;
            }
            return true;
        }

        internal static void Spawned(Player player)
        {
            if (enabled?.Value!=true || !Scope(player) || ReferenceEquals(lastSpawn,player)) return;
            lastSpawn=player;Queue(player,"spawn");
        }

        internal static void Retrieved(long sourceWorld,long sourcePlayer,string tombId)
        {
            var player=Player.m_localPlayer;
            if (enabled?.Value!=true || !Scope(player) || sourceWorld!=world || sourcePlayer!=playerId
                || string.IsNullOrEmpty(tombId) || tombId.Length>128 || player.IsDead()) return;
            if (graves.Contains(tombId)) return;
            // Fail closed at capacity; a world/player change clears the session's bounded duplicate history.
            if (graves.Count>=256) { lastResult="retrieval-history-full";return; }
            graves.Add(tombId);Queue(player,"retrieval");
        }

        static void Queue(Player player,string reason)
        {
            if (eating) return;
            actor=player;pending=true;deadline=Time.realtimeSinceStartup+180f;nextCheck=0f;lastResult="waiting-"+reason;
        }

        // Every two seconds, queue a normal eating pass when a Food-slot item could be eaten now.
        static void AutoCheck()
        {
            var player=Player.m_localPlayer;
            if (AutoFromSlots?.Value!=true || eating || !Plugin.Ready || !Plugin.AllowsUi || !player || player.IsDead()) return;
            float now=Time.realtimeSinceStartup;
            if (now<nextAuto) return;
            nextAuto=now+2f;
            if (!Scope(player)) return;
            var inventory=player.GetInventory();
            if (!MantleFoodIntegration.TryFoodCells(inventory,out var cells)) return;
            foreach(var cell in cells)
            {
                var item=inventory.GetItemAt(cell.Item1,cell.Item2);
                if(item?.m_shared!=null && item.m_stack>0 && item.m_shared.m_itemType==ItemDrop.ItemData.ItemType.Consumable
                    && (item.m_shared.m_food>0f || item.m_shared.m_foodStamina>0f || item.m_shared.m_foodEitr>0f) && player.CanEat(item,false))
                { Queue(player,"auto");return; }
            }
        }

        internal static void Tick()
        {
            if (!pending) AutoCheck();
            if (!pending) return;
            if (enabled?.Value!=true || !Plugin.Ready || !Plugin.AllowsUi || !actor || actor!=Player.m_localPlayer
                || !ZNet.instance || ZNet.instance.GetWorld()==null || ZNet.instance.GetWorldUID()!=world
                || actor.GetPlayerID()!=playerId || actor.IsDead()) { pending=false;lastResult="cancelled";return; }
            float now=Time.realtimeSinceStartup;
            if (now>=deadline) { pending=false;lastResult="readiness-timeout";return; }
            if(now<nextCheck)return;nextCheck=now+0.1f;
            var view=actor.GetComponent<ZNetView>();var game=Game.instance;var profile=game ? game.GetPlayerProfile() : null;
            if (!view || !view.IsValid() || !view.IsOwner() || profile==null || profile.GetPlayerID()!=playerId
                || !CruciblePlugin.Session.AuthorityReady || CruciblePlugin.Session.WorldId!=world
                || actor.InCutscene() || actor.IsTeleporting() || actor.InAttack() || actor.InDodge()
                || actor.m_wakeupTimer>=0f || !actor.m_animator || actor.m_animator.GetBool("wakeup")
                || view.GetZDO()==null || view.GetZDO().GetBool(ZDOVars.s_wakeup,true)) return;
            var inventory=actor.GetInventory();
            if (!MantleFoodIntegration.TryFoodCells(inventory,out var cells)) return;
            pending=false;eating=true;
            try
            {
                foreach(var cell in cells)
                {
                    if(actor!=Player.m_localPlayer || actor.IsDead() || !ReferenceEquals(inventory,actor.GetInventory()))break;
                    var item=inventory.GetItemAt(cell.Item1,cell.Item2);
                    if(item?.m_shared==null || item.m_stack<=0 || item.m_shared.m_itemType!=ItemDrop.ItemData.ItemType.Consumable
                        || !(item.m_shared.m_food>0f || item.m_shared.m_foodStamina>0f || item.m_shared.m_foodEitr>0f)
                        || !actor.CanEat(item,false))continue;
                    int before=item.m_stack;attempts++;
                    actor.UseItem(inventory,item,true);
                    int after=inventory.ContainsItem(item)?item.m_stack:0;
                    if(after<before)consumed+=before-after;
                }
                lastResult="completed";
            }
            finally { eating=false; }
        }

        internal static object Status()=>new {enabled=enabled?.Value==true,auto=AutoFromSlots?.Value==true,pending,world,playerId,lastResult,attempts,consumed,graveHistory=graves.Count};
        internal static void Shutdown()
        {
            patches?.UnpatchSelf();patches=null;enabled=null;AutoFromSlots=null;nextAuto=0;pending=eating=false;actor=lastSpawn=null;
            world=playerId=0;graves.Clear();deadline=nextCheck=0;attempts=consumed=0;lastResult="idle";
        }

        [HarmonyPatch(typeof(Player),nameof(Player.OnSpawned))]
        static class SpawnPatch
        {
            [HarmonyPostfix] static void After(Player __instance,bool __runOriginal)
            { if(__runOriginal && Plugin.AllowsUi)Spawned(__instance); }
        }
    }
}
