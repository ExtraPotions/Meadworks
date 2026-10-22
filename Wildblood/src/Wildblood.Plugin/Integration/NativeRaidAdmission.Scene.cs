// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime {
    internal static partial class NativeRaidAdmission {
        sealed class SceneLease {internal ZDO Chest=null!;internal float Expires;internal bool Ready;}
        static readonly Dictionary<ZDOID,SceneLease> sceneLeases=new Dictionary<ZDOID,SceneLease>();
        static readonly List<ZDO> sceneObjects=new List<ZDO>();
        static readonly SimulationDistance sceneDistance=new SimulationDistance(2,0,true);
        static long sceneGeneration=-1;static float nextSceneTick;
        static bool SceneNear(Vector3 position)=>Authority&&sceneLeases.Values.Any(l=>(l.Chest.GetPosition()-position).sqrMagnitude<=128f*128f);
        static bool LeaseScene(ZDO chest){
            if(!ZNet.instance.IsDedicated())return true;
            if(sceneGeneration!=CruciblePlugin.Session.Generation){sceneLeases.Clear();deferred.Clear();sceneGeneration=CruciblePlugin.Session.Generation;}
            if(sceneLeases.TryGetValue(chest.m_uid,out var lease)){lease.Expires=Time.unscaledTime+30;return lease.Ready;}
            if(sceneLeases.Count>=8||!ZoneSystem.instance||!ZNetScene.instance)return false;
            sceneLeases.Add(chest.m_uid,new SceneLease{Chest=chest,Expires=Time.unscaledTime+30});return false;
        }
        static bool RunningLease(SceneLease lease){
            var chest=FindChest(lease.Chest.m_uid);var runner=chest?chest.GetComponent<ExtraPotions.Wildblood.Adopted.Raids.RaidRunner>():null;
            return runner&&runner.State.Running&&Valid(chest)&&CanRun(runner);
        }
        static void SceneTick(){
            if(!Authority||!ZNet.instance.IsDedicated()){sceneLeases.Clear();return;}
            if(sceneGeneration!=CruciblePlugin.Session.Generation){sceneLeases.Clear();deferred.Clear();sceneGeneration=CruciblePlugin.Session.Generation;}
            if(Time.unscaledTime<nextSceneTick)return;nextSceneTick=Time.unscaledTime+.1f;
            var zones=ZoneSystem.instance;var scene=ZNetScene.instance;if(!zones||!scene||ZDOMan.instance==null)return;
            foreach(var pair in sceneLeases.ToArray()){
                var lease=pair.Value;
                if(ZDOMan.instance.GetZDO(pair.Key)!=lease.Chest){sceneLeases.Remove(pair.Key);continue;}
                if(RunningLease(lease))lease.Expires=Time.unscaledTime+10;
                if(Time.unscaledTime>lease.Expires){sceneLeases.Remove(pair.Key);continue;}
                var center=ZoneSystem.GetZone(lease.Chest.GetPosition());bool terrain=true,spawned=false;
                for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++){
                    var sector=new Vector2s(center.x+x,center.y+y);
                    if(!zones.IsZoneGenerated(sector)){terrain=false;continue;}
                    if(zones.IsZoneLoaded(sector))zones.PokeLocalZone(sector);
                    else {terrain=false;if(!spawned)spawned=zones.PokeLocalZone(sector);}
                }
                sceneObjects.Clear();ZDOMan.instance.FindSectorObjects(center,sceneDistance,sceneObjects);
                lease.Ready=terrain&&sceneObjects.Count<=16384&&sceneObjects.All(z=>scene.HaveInstance(z)||!scene.GetPrefab(z.GetPrefab()));
            }
        }
        static void AppendScene(List<ZDO> near){
            if(!Authority||!ZNet.instance.IsDedicated()||sceneLeases.Count==0||!ZoneSystem.instance)return;
            var present=new HashSet<ZDO>(near);
            foreach(var lease in sceneLeases.Values){
                sceneObjects.Clear();ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(lease.Chest.GetPosition()),sceneDistance,sceneObjects);
                if(sceneObjects.Count>16384)continue;
                foreach(var zdo in sceneObjects){
                    var zone=zdo.GetSector();
                    if(ZoneSystem.instance.IsZoneLoaded(zone)&&ZoneSystem.instance.IsZoneReadyForType(zone,zdo.Type)&&present.Add(zdo))near.Add(zdo);
                }
            }
        }
        static void CloseScenes(){sceneLeases.Clear();deferred.Clear();sceneObjects.Clear();sceneGeneration=-1;nextSceneTick=0;}
        [HarmonyPatch(typeof(ZNetScene),"CreateObjects")]
        static class RaidSceneObjects {static void Prefix(List<ZDO> currentNearObjects)=>AppendScene(currentNearObjects);}
        [HarmonyPatch(typeof(ZNetScene),"RemoveObjects")]
        static class RaidSceneRetention {static void Prefix(List<ZDO> currentNearObjects)=>AppendScene(currentNearObjects);}
        [HarmonyPatch(typeof(ZNetScene),"OutsideActiveArea",new[]{typeof(Vector3)})]
        static class RaidSceneActive {static void Postfix(Vector3 point,ref bool __result){if(__result&&SceneNear(point))__result=false;}}
        [HarmonyPatch(typeof(ZDO),"SetOwner")]
        static class RaidSceneOwnership {
            static bool Prefix(ZDO __instance,long uid){
                if(!ZNet.instance||!ZNet.instance.IsServer()||ZDOMan.instance==null||ZDOMan.instance.GetZDO(__instance.m_uid)!=__instance
                    ||uid==ZNet.GetUID()||__instance.GetOwner()!=ZNet.GetUID()||!Canonical(__instance.GetString(NativeRaidChest.TokenKey))
                    ||!__instance.GetBool(NativeRaidChest.HoldKey)&&!ExtraPotions.Wildblood.Adopted.Raids.RaidState.IsRunning(__instance))return true;
                if(FindChest(__instance.m_uid))return false;
                var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(__instance.GetPrefab()):null;
                return !prefab||!prefab.GetComponent<Container>();
            }
        }
    }
}
