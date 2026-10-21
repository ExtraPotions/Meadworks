using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 [HarmonyPatch(typeof(Player),"TryPlacePiece")]
 static class PlacementInputPatch {
  static bool Prefix(Player __instance,Piece piece,ref bool __result){
   var plugin=Plugin.Instance;var controller=plugin?.Planting;var preview=plugin?.Preview;
   if(!plugin||!plugin.GameplayAvailable||__instance!=Player.m_localPlayer||controller==null||preview==null||!controller.CanHandle(piece))return true;
   // The queued transactions own every charge, so the native placement never runs for handled crops.
   __result=false;if(InputController.Blocked()||controller.IsPlanting)return false;
   var ghost=(GameObject?)NativeFields.PlacementGhost.GetValue(__instance);if(!ghost||!ghost.activeSelf)return false;
   var status=(Player.PlacementStatus)NativeFields.PlacementStatus.GetValue(__instance);
   bool growthFailure=status==Player.PlacementStatus.WrongBiome||status==Player.PlacementStatus.NeedCultivated||status==Player.PlacementStatus.NeedDirt||status==Player.PlacementStatus.MoreSpace||status==Player.PlacementStatus.DeepSnow||status==Player.PlacementStatus.NoSnow;
   if(status!=Player.PlacementStatus.Valid&&!(plugin.Settings!.UnrestrictedPlacement&&growthFailure)){if(growthFailure)plugin.Settings.NotifyUnrestrictedBlocked();return false;}
   // Plant the grid the player saw: the preview keeps its snapped row/column directions, which a fresh unsnapped layout at the same root would not. Recompute only when the preview no longer matches the ghost.
    var current=preview.Current;var layout=current!=null&&current.Piece==piece&&current.Cells.Length==preview.CellCount(piece)&&(current.Cells[0]-ghost.transform.position).sqrMagnitude<0.0001f&&Quaternion.Angle(current.Rotation,ghost.transform.rotation)<0.01f?current:preview.Compute(__instance,piece,ghost.transform.position,ghost.transform.rotation,false);preview.EvaluateAll(__instance,layout,ghost);
   var plan=controller.Plan(__instance,layout);
   // A refused grid is still recorded as one fully canceled batch so callers observe the refusal; nothing is charged.
   if(plan.Refused)__instance.Message(MessageHud.MessageType.Center,GridPreview.Message(plan.Reason));
   controller.SubmitFromGame(piece,layout,plan);
   return false;
  }
 }
}
