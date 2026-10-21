// Adapted from BetterTombstoneHints 1.0.0 by JevMods (https://github.com/JevMods/BetterTombstoneHints, commit 808f97ed),
// Copyright (c) 2026 JevMods, MIT licence (licenses/BetterTombstoneHints-MIT.txt). Adapted parts: CauseKeys, DeathNote,
// LocalPlayerDeath's cause decisions, NoteRenderer and HoverNote. Wayfarer changes: game-free decisions, the game's own
// stat_DeathBy* strings for the cause texts (every game language, no second translation layer), sanitised and bounded stored names, and C09 step-aside. Modifications GPL-3.0-or-later.
using System;
using System.Collections.Generic;
using System.Linq;
namespace Wayfarer.Core {
    /// <summary>A tombstone's death note: cause key, killer (game token or player name) and day.</summary>
    public sealed class TombstoneNote {
        public TombstoneNote(string cause,string killer,int day){Cause=cause??"";Killer=killer??"";Day=Math.Max(0,day);}
        public string Cause {get;}
        public string Killer {get;}
        public int Day {get;}
    }

    /// <summary>Client-side tombstone hints: "Slain by a Troll, Day 42" on the hover text and the nameplate.</summary>
    public static class TombstoneHints {
        public const bool DefaultEnabled=true;
        public const string SourceGuid="JevMods.BetterTombstoneHints",SourceName="BetterTombstoneHints",ModuleId="tombstone_hints";
        /// <summary>The upstream ZDO fields, kept so notes written by either mod show in both.</summary>
        public const string CauseField="JevMods_Cause",KillerField="JevMods_Killer",DayField="JevMods_Day";
        public const int MaxName=64;
        public const string Died="died",Slain="slain",SlainBy="slain_by",SlainByCreature="slain_by_creature";
        static readonly Dictionary<string,string> byHitType=new Dictionary<string,string>(StringComparer.Ordinal) {
            {"Fall","fall"},{"Drowning","drowning"},{"Burning","burning"},{"Freezing","freezing"},{"Poisoned","poisoned"},
            {"Water","water"},{"Smoke","smoke"},{"EdgeOfWorld","edge_of_world"},{"Impact","impact"},{"Cart","cart"},
            {"Tree","tree"},{"Self","self"},{"Structural","structural"},{"Turret","turret"},{"Boat","boat"},
            {"Stalagtite","stalactite"},{"Catapult","catapult"},{"CinderFire","cinder_fire"},{"AshlandsOcean","ashlands_ocean"},
            {"AshlandsLava","ashlands_lava"},{"Incinerator","incinerator"},{"DrawBridge","drawbridge"}};
        /// <summary>Every cause key, in the upstream language-file order.</summary>
        public static readonly IReadOnlyList<string> Causes=Array.AsReadOnly(new[]{SlainByCreature,SlainBy,Slain,Died}.Concat(byHitType.Values).ToArray());

        /// <summary>The cause's former catalog key; kept only so tests can prove the catalog no longer carries it.</summary>
        public static string Key(string cause)=>"tomb_"+cause;
        static readonly Dictionary<string,string> nativeByCause=byHitType.ToDictionary(p=>p.Value,p=>"$stat_DeathBy"+p.Key,StringComparer.Ordinal);
        /// <summary>
        /// The game's own localization token for a cause (PlayerStatType texts). The game has none for Ashlands lava (burning
        /// is used) or for "died" (empty: the note is the day alone).
        /// </summary>
        public static string NativeKey(string cause) {
            if(cause==SlainByCreature||cause==SlainBy||cause==Slain)return "$stat_DeathByEnemyHit";
            if(cause=="ashlands_lava")return "$stat_DeathByBurning";
            return cause!=null&&nativeByCause.TryGetValue(cause,out var key)?key:"";
        }
        public static string ForHitType(string hitType)=>hitType!=null&&byHitType.TryGetValue(hitType,out var key)?key:Died;

        /// <summary>
        /// The note for the local player's death. <paramref name="hitType"/> is the last hit's HitData.HitType name (null:
        /// no last hit); <paramref name="attacker"/> is null when the attacker is gone.
        /// </summary>
        public static TombstoneNote Death(string? hitType,object? attacker,bool attackerIsPlayer,bool attackerIsBoss,string attackerName,int day) {
            if(hitType==null)return new TombstoneNote(Died,"",day);
            if(hitType=="EnemyHit"||hitType=="PlayerHit") {
                if(attacker==null)return new TombstoneNote(Slain,"",day);
                return new TombstoneNote(attackerIsPlayer||attackerIsBoss?SlainBy:SlainByCreature,Clean(attackerName),day);
            }
            return new TombstoneNote(ForHitType(hitType),"",day);
        }

        /// <summary>
        /// "cause: killer, day" in the game language. <paramref name="localize"/> is the game's Localization (game tokens,
        /// plain names unchanged); <paramref name="dayText"/> is the game's localized day (empty: no day).
        /// </summary>
        public static string Render(TombstoneNote note,Func<string,string> localize,string dayText) {
            var cause=Causes.Contains(note.Cause)?note.Cause:Died;
            var token=NativeKey(cause);
            var text=token.Length==0?"":Clean(localize(token),256);
            if(text.StartsWith("[",StringComparison.Ordinal)&&text.EndsWith("]",StringComparison.Ordinal))text=""; // the game lacks the string
            var killer=Clean(localize(Clean(note.Killer)));
            if(killer.Length>0)text=text.Length==0?killer:text+": "+killer;
            var day=Clean(dayText);
            return day.Length==0?text:text.Length==0?day:text+", "+day;
        }

        /// <summary>Inserts the note after the hover text's first line (the tombstone's name).</summary>
        public static string Insert(string hoverText,string note) {
            if(hoverText.Length==0||note.Length==0)return hoverText;
            var end=hoverText.IndexOf('\n');
            return end<0?hoverText+"\n"+note:hoverText.Insert(end,"\n"+note);
        }

        public static bool StepsAside(IEnumerable<string> installedGuids)=>installedGuids.Contains(SourceGuid,StringComparer.Ordinal);

        /// <summary>Stored fields come from any client: no rich-text tags or line breaks, bounded length.</summary>
        static string Clean(string? value,int max=MaxName) {
            if(string.IsNullOrEmpty(value))return "";
            var cleaned=new string(value!.Where(c=>c!='<'&&c!='>'&&c!='\r'&&c!='\n').ToArray()).Trim();
            return cleaned.Length>max?cleaned.Substring(0,max):cleaned;
        }
    }
}
