// PlaceAnywhere lifecycle adapted from PlantEverything 1.21.3, Advize_ValheimMods commit
// 1deeb3962eb8681394961b0f35a4dfbc315c6f99. See THIRD_PARTY_NOTICES/PlantEverything.txt.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>Persists native support for a correlated PlaceAnywhere object across growth and reload.</summary>
 internal static class PlaceAnywhere {
  internal const string Marker="wildroot.placeAnywhere";
  static bool Active=>Plugin.Instance&&Plugin.Instance.GameplayAvailable&&Plugin.Instance.Settings?.Active==true;
  static readonly MethodInfo PropagateMethod=AccessTools.Method(typeof(PlaceAnywhere),nameof(Propagate));

  // Called synchronously after the existing receipt scope proves one valid correlated owner object.
  // The marker describes placement support, not payment: pending receipt guards still block growth/yield,
  // and the existing rollback removes this same object if the payment does not commit.
  internal static void MarkPlaced(GameObject created){
   if(!Active||!created)return;
   var view=created.GetComponent<ZNetView>();var physics=created.GetComponent<StaticPhysics>();
   if(!physics||!view||!view.IsValid()||!view.IsOwner())return;
   physics.m_fall=false;view.GetZDO().Set(Marker,true);
  }

  static void Propagate(Plant plant,GameObject grownTree){
   if(!Active||!plant||!grownTree)return;
   var source=plant.GetComponent<ZNetView>();
   if(!source||!source.IsValid()||!source.GetZDO().GetBool(Marker))return;
   var tree=grownTree.GetComponent<TreeBase>();var physics=tree?tree.GetComponent<StaticPhysics>():null;
   var view=tree?tree.GetComponent<ZNetView>():null;
   if(!physics||!view||!view.IsValid()||!view.IsOwner())return;
   physics.m_fall=false;view.GetZDO().Set(Marker,true);
  }

  // Native Awake may run before the session handshake. Restore the saved object's support
  // flag without waiting for placement authority; master/step-aside and payment guards remain.
  // Changing PlaceAnywhere does not erase the original placement setting.
  static void Restore(Component instance){
   if(Plugin.Instance?.Ready!=true||Plugin.Instance.Settings?.SteppedAside!=false||Plugin.Instance.Settings.Enabled.Value!=true||!instance)return;
   var view=instance.GetComponent<ZNetView>();var physics=instance.GetComponent<StaticPhysics>();
   if(physics&&view&&view.IsValid()&&view.GetZDO().GetBool(Marker))physics.m_fall=false;
  }

  [HarmonyPatch(typeof(Plant),"Grow")]
  static class GrowPatch {
   // Retain the pinned reference's native TreeBase.Grow insertion point and grown-object local.
   // The bundled HarmonyX constructor references Mono's ILGenerator facade; reflection bridges
   // that constructor on netstandard2.1 without replacing the reference's native matcher.
   static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)=>((CodeMatcher)Activator.CreateInstance(typeof(CodeMatcher),new object?[]{instructions,null})!)
    .MatchForward(false,new CodeMatch(OpCodes.Callvirt,AccessTools.Method(typeof(TreeBase),nameof(TreeBase.Grow))))
    .ThrowIfInvalid("Could not patch Plant.Grow (Wildroot PlaceAnywhere)")
    .Advance(1)
    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Ldloc_1),new CodeInstruction(OpCodes.Call,PropagateMethod))
    .InstructionEnumeration();
  }
  [HarmonyPatch(typeof(Piece),"Awake")]
  static class PieceReloadPatch {static void Postfix(Piece __instance)=>Restore(__instance);}
  [HarmonyPatch(typeof(TreeBase),"Awake")]
  static class TreeReloadPatch {static void Postfix(TreeBase __instance)=>Restore(__instance);}
 }
}
