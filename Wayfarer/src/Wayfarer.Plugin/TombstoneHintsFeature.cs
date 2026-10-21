// Adapted from BetterTombstoneHints 1.0.0 by JevMods (https://github.com/JevMods/BetterTombstoneHints, commit 808f97ed),
// Copyright (c) 2026 JevMods, MIT licence (licenses/BetterTombstoneHints-MIT.txt). Adapted parts: TombStonePatches,
// LocalPlayerDeath, TombstoneNotes, NoteDisplayFeature and TombstoneNameplate. Wayfarer changes: a Wayfarer setting
// (default on, client-side), owner-only writes, C09 step-aside and the game's own localized death strings.
// Modifications GPL-3.0-or-later.
using System;
using System.Globalization;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Wayfarer.Core;
namespace Wayfarer {
 /// <summary>Client-side cause of death and day on tombstones; steps aside when BetterTombstoneHints is installed.</summary>
 internal static class TombstoneHintsFeature {
  static readonly int causeKey=TombstoneHints.CauseField.GetStableHashCode(),killerKey=TombstoneHints.KillerField.GetStableHashCode(),dayKey=TombstoneHints.DayField.GetStableHashCode();
  static readonly AccessTools.FieldRef<TombStone,ZNetView> nview=AccessTools.FieldRefAccess<TombStone,ZNetView>("m_nview");
  static readonly AccessTools.FieldRef<Character,HitData> lastHit=AccessTools.FieldRefAccess<Character,HitData>("m_lastHit");
  static readonly System.Reflection.MethodInfo? currentDay=AccessTools.Method(typeof(EnvMan),"GetCurrentDay");
  static bool? steppedAside;

  /// <summary>Decided at first use in play, after every plugin has loaded, so a later-loading BetterTombstoneHints is seen.</summary>
  static bool SteppedAside(Plugin p) {
   if(steppedAside==null){
    steppedAside=TombstoneHints.StepsAside(Chainloader.PluginInfos.Keys);
    if(steppedAside.Value)p.Integration.ReportStepAside(TombstoneHints.ModuleId,TombstoneHints.SourceGuid,TombstoneHints.SourceName);
   }
   return steppedAside.Value;
  }
  internal static bool Active() {
   var p=Plugin.Instance;
   return p!=null&&p.Options!=null&&p.Options.Enabled.Value&&p.Options.TombstoneHints.Value&&!SteppedAside(p);
  }
  internal static bool HasZdo(TombStone tombstone){var view=nview(tombstone);return view!=null&&view.IsValid();}
  static string Localize(string text)=>Localization.instance!=null?Localization.instance.Localize(text):text;

  /// <summary>The dying player's client owns the new tombstone and writes the note into the upstream ZDO fields.</summary>
  internal static void Write(TombStone tombstone) {
   var view=nview(tombstone);var player=Player.m_localPlayer;
   if(view==null||!view.IsValid()||!view.IsOwner()||player==null||EnvMan.instance==null)return;
   var day=currentDay?.Invoke(EnvMan.instance,null) is int d?d:0;
   var hit=lastHit(player);var attacker=hit?.GetAttacker();
   var name=attacker is Player killer?killer.GetPlayerName():attacker!=null?attacker.m_name:"";
   var note=TombstoneHints.Death(hit==null?null:hit.m_hitType.ToString(),attacker,attacker is Player,attacker!=null&&attacker.IsBoss(),name,day);
   var zdo=view.GetZDO();zdo.Set(causeKey,note.Cause);zdo.Set(killerKey,note.Killer);zdo.Set(dayKey,note.Day);
  }

  /// <summary>The note in the reader's game language, or null when the tombstone has none.</summary>
  internal static string? Read(TombStone tombstone) {
   var view=nview(tombstone);var p=Plugin.Instance;
   if(p==null||view==null||!view.IsValid())return null;
   var zdo=view.GetZDO();var cause=zdo.GetString(causeKey);
   if(cause.Length==0)return null;
   var note=new TombstoneNote(cause,zdo.GetString(killerKey),zdo.GetInt(dayKey));
   var day=Localization.instance!=null?Localization.instance.Localize("$msg_newday",note.Day.ToString(CultureInfo.InvariantCulture)):"";
   return TombstoneHints.Render(note,Localize,day);
  }
 }

 /// <summary>Second nameplate line under the owner's name; restores the plain name when hints turn off.</summary>
 internal sealed class TombstoneNameplate:MonoBehaviour {
  TombStone? tombstone;TextWrappingModes wrapping;float next;bool shown;
  internal void Initialize(TombStone owner){tombstone=owner;wrapping=owner.m_worldText.textWrappingMode;}
  void Update() {
   if(tombstone==null||Time.time<next)return;
   next=Time.time+.5f;
   try {
    var note=TombstoneHintsFeature.Active()?TombstoneHintsFeature.Read(tombstone):null;
    if(note==null){if(shown){Restore();shown=false;}return;}
    tombstone.m_worldText.textWrappingMode=TextWrappingModes.NoWrap;
    if(Readable()){Show(tombstone.GetOwnerName()+"\n"+note);shown=true;}
   }catch(Exception error){enabled=false;Plugin.Instance?.Error(error);}
  }
  /// <summary>TombStone.GetOwnerName reads the ZDO, which ZNetScene clears before destroying the object.</summary>
  bool Readable()=>tombstone!=null&&TombstoneHintsFeature.HasZdo(tombstone);
  void OnDestroy(){if(shown&&Readable())Restore();}
  void Restore(){if(tombstone==null)return;tombstone.m_worldText.textWrappingMode=wrapping;if(Readable())Show(tombstone.GetOwnerName());}
  void Show(string text){if(tombstone!=null&&tombstone.m_worldText.text!=text)tombstone.m_worldText.text=text;}
 }

 [HarmonyPatch]
 internal static class TombstonePatches {
  [HarmonyPatch(typeof(TombStone),nameof(TombStone.Setup)),HarmonyPostfix]
  static void Setup(TombStone __instance) {
   try{if(TombstoneHintsFeature.Active())TombstoneHintsFeature.Write(__instance);}
   catch(Exception error){Plugin.Instance?.Error(error);}
  }
  [HarmonyPatch(typeof(TombStone),"Start"),HarmonyPostfix]
  static void Start(TombStone __instance) {
   try{if(Plugin.Instance!=null&&__instance.GetComponent<TombstoneNameplate>()==null)__instance.gameObject.AddComponent<TombstoneNameplate>().Initialize(__instance);}
   catch(Exception error){Plugin.Instance?.Error(error);}
  }
  [HarmonyPatch(typeof(TombStone),nameof(TombStone.GetHoverText)),HarmonyPostfix]
  static void GetHoverText(TombStone __instance,ref string __result) {
   try{
    if(__result.Length==0||!TombstoneHintsFeature.Active())return;
    var note=TombstoneHintsFeature.Read(__instance);
    if(note!=null)__result=TombstoneHints.Insert(__result,note);
   }catch(Exception error){Plugin.Instance?.Error(error);}
  }
 }
}
