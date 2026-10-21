// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;

namespace ExtraPotions.Wildroot.Runtime {
 [HarmonyPatch(typeof(Player),"CheckCanRemovePiece")]
 internal static class CultivatorRemovalPatch {
  internal static bool Prefix(Player __instance,Piece piece,ref bool __result){
   var plugin=Plugin.Instance;
   if(!plugin||!plugin.GameplayAvailable||plugin.Settings?.Active!=true||__instance!=Player.m_localPlayer)return true;
   var tool=PlacementController.RightItem(__instance);
   if(tool?.m_dropPrefab==null||tool.m_dropPrefab.name!="Cultivator")return true;
   var catalog=plugin.Catalog;
   if(piece&&piece.IsPlacedByPlayer()&&catalog!=null&&catalog.Definitions.TryGetValue(NativePlanting.BasePrefabName(piece.gameObject),out var definition)&&definition.Enabled)return true;
   // Native RemovePiece retains its ray/range, ward, station, removal-state and resource checks.
   __result=false;return false;
  }
 }
}
