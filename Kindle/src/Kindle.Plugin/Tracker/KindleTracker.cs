// Changed by expDARE, 2026: retained source adaptation notices.
// Kindle - Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from OpenKeep 3.0.0, ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// OpenKeep/OpenKeep/src/Tracker/{TrackerList,TrackedRecipe,TrackerVisibility,TrackerHud}.cs
// and Core/CharacterData.cs. OpenKeep - Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE: shared native recipe index, Kindle config/data/words and kit HUD lifecycle.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Kindle.Runtime
{
    public static class KindleTracker
    {
        public const string DataKey="Kindle.trackedRecipes";
        private static readonly List<TrackedRecipe> entries=new List<TrackedRecipe>();
        private static Player? loadedFor;
        private static KindleConfig? settings;
        private static MeadworksLog? log;
        private static Harmony? patches;
        private static TrackerPanel? panel;
        private static float lastThreat=-100f,nextCount;
        private static int version,builtVersion=-1;
        private static string builtStyle="";
        internal static KindleConfig Settings=>settings!;
        internal static IReadOnlyList<TrackedRecipe> Entries {get{Load();return entries;}}
        internal static bool Active=>Plugin.Ready && Plugin.AllowsUi && settings!=null && ZNet.instance
            && Player.m_localPlayer && !Player.m_localPlayer.IsDead();
        public static bool NearbyAvailable=>TrackerCounts.NearbyAvailable;

        public static void Initialize(KindleConfig config,MeadworksLog logger)
        {
            Shutdown();settings=config;log=logger;
            if(Plugin.AllowsUi){patches=new Harmony(Plugin.Guid+".tracker");patches.CreateClassProcessor(typeof(TrackerCraftWatch)).Patch();}
        }
        public static void Shutdown()
        {
            patches?.UnpatchSelf();patches=null;
            panel?.Dispose();panel=null;TrackerCounts.Shutdown();TrackerStyle.Shutdown();
            entries.Clear();loadedFor=null;settings=null;log=null;builtVersion=-1;builtStyle="";
            nextCount=0;lastThreat=-100f;
        }
        private static void Load()
        {
            var player=Player.m_localPlayer;
            if(ReferenceEquals(loadedFor,player))return;
            loadedFor=player;entries.Clear();version++;lastThreat=-100f;TrackerCounts.Shutdown();
            if(player && player.m_customData.TryGetValue(DataKey,out var value))
                foreach(var text in value.Split(',').Select(part=>part.Trim()).Distinct(StringComparer.Ordinal))
                {
                    var tracked=TrackedRecipe.Parse(text);
                    if(tracked!=null && !entries.Any(item=>item.Is(tracked.Key,tracked.Quality,tracked.Upgrader)))entries.Add(tracked);
                }
        }
        private static void Save()
        {
            if(!Active || !ReferenceEquals(loadedFor,Player.m_localPlayer))return;
            if(entries.Count==0)loadedFor!.m_customData.Remove(DataKey);
            else loadedFor!.m_customData[DataKey]=string.Join(",",entries.Select(item=>item.ToString()));
            version++;
        }
        private static int IndexOf(Recipe recipe,int quality,bool upgrader)
        {
            if(!recipe)return -1;Load();string key=KindleRecipes.Key(recipe);
            return entries.FindIndex(entry=>entry.Is(key,quality,upgrader));
        }
        public static bool IsTracked(Recipe recipe,int quality,bool upgrader)=>IndexOf(recipe,quality,upgrader)>=0;
        public static void Toggle(Recipe recipe,int quality,int amount,bool upgrader)
        {
            if(!Active || !settings!.Get(KindleSettings.Tracker.Enabled) || !recipe || !recipe.m_item)return;
            int index=IndexOf(recipe,quality,upgrader);
            if(index>=0){Remove(index);return;}
            if(entries.Count>=settings.Get(KindleSettings.Tracker.MaxTracked))
            {Message(KindleText.Format("tracker_full",settings.Get(KindleSettings.Tracker.MaxTracked)));return;}
            string key=KindleRecipes.Key(recipe);if(key.Length==0)return;
            entries.Add(new TrackedRecipe(key,quality,amount,upgrader));Save();
            Message(KindleText.Format("tracker_tracking",DisplayName(recipe,quality)));
        }
        internal static void Remove(int index)
        {
            Load();if(!Active || index<0 || index>=entries.Count)return;
            var entry=entries[index];entries.RemoveAt(index);Save();
            if(entry.Recipe)Message(KindleText.Format("tracker_untracked",DisplayName(entry.Recipe!,entry.Quality)));
        }
        internal static void Step(int index,int direction,bool tens)
        {
            Load();if(!Active || index<0 || index>=entries.Count)return;
            int amount=entries[index].Amount;
            int target=!tens ? amount+direction : direction>0 ? (amount/10+1)*10 : (amount-1)/10*10;
            entries[index].Amount=Mathf.Clamp(target,1,TrackedRecipe.MaxAmount);Save();
        }
        public static void Crafted(Recipe recipe,int quality,int crafts,bool upgrader)
        {
            if(!Active || !settings!.Get(KindleSettings.Tracker.UntrackWhenCrafted))return;
            int index=IndexOf(recipe,quality,upgrader);if(index<0)return;
            entries[index].Amount-=Mathf.Max(1,crafts);
            if(entries[index].Amount<=0){entries.RemoveAt(index);Message(KindleText.Format("tracker_made",DisplayName(recipe,quality)),true);}
            Save();
        }
        internal static string DisplayName(Recipe recipe,int quality)
        {
            if (recipe == null) return string.Empty;
            string rawName = recipe.m_item?.m_itemData?.m_shared?.m_name ?? recipe.m_item?.gameObject?.name ?? recipe.name ?? string.Empty;
            string name = Localization.instance != null ? Localization.instance.Localize(rawName) : rawName;
            return quality>1 ? KindleText.Format("tracker_upgrade",name,quality) : name;
        }
        private static void Message(string text,bool top=false) => KindleText.Notice(text,top);
        /// <summary>The game's language changed: the tracker is written again in it now.</summary>
        internal static void Relabel(){builtVersion=-1;Tick();}
        public static void Tick()
        {
            if(settings==null)return;Load();
            var hud=Hud.instance ? Hud.instance.m_rootObject : null;
            if(panel!=null && !panel.BelongsTo(hud ? hud.transform : null)){panel.Dispose();panel=null;builtVersion=-1;}
            bool show=Active && settings.Get(KindleSettings.Tracker.Enabled) && entries.Count>0;
            if(Active && Player.m_localPlayer.IsTargeted())lastThreat=Time.time;
            if(settings.Get(KindleSettings.Tracker.HideWithMap) && Minimap.IsOpen())show=false;
            if(settings.Get(KindleSettings.Tracker.HideInCombat) && Time.time-lastThreat<5f)show=false;
            if(panel==null && show && hud){panel=TrackerPanel.Create(hud.transform);builtVersion=-1;}
            if(panel==null)return;
            bool free=ZCursor.LockState!=CursorLockMode.Locked && ZCursor.IsVisible;
            panel.Update(show,free);if(!show)return;
            if(builtVersion!=version){panel.Rebuild(entries);builtVersion=version;nextCount=0;}
            if(Time.unscaledTime<nextCount)return;
            nextCount=Time.unscaledTime+1.25f;
            try
            {
                string style=TrackerStyle.Signature;
                if(style!=builtStyle){builtStyle=style;panel.Rebuild(entries);}
                TrackerCounts.Tick(entries);panel.Count();
            }
            catch(Exception error){log?.Warning("Tracker refresh refused: "+error.GetType().Name);}
        }
    }

    internal sealed class TrackedRecipe
    {
        internal const int MaxAmount=9999;
        internal string Key {get;}
        internal int Quality {get;}
        internal bool Upgrader {get;}
        internal int Amount {get;set;}
        internal Recipe? Recipe=>KindleRecipes.Find(Key);
        internal TrackedRecipe(string key,int quality,int amount,bool upgrader)
        {Key=key;Quality=Math.Max(1,quality);Amount=Mathf.Clamp(amount,1,MaxAmount);Upgrader=upgrader;}
        internal bool Is(string key,int quality,bool upgrader)=>Key==key && Quality==quality && Upgrader==upgrader;
        public override string ToString()=>Key+"|"+Quality.ToString(CultureInfo.InvariantCulture)+"|"+Amount.ToString(CultureInfo.InvariantCulture)+(Upgrader ? "|u" : "");
        internal static TrackedRecipe? Parse(string text)
        {
            var parts=(text??"").Split('|');
            if(parts.Length<3 || parts.Length>4 || parts[0].Length==0
                || !int.TryParse(parts[1],out int quality) || !int.TryParse(parts[2],out int amount))return null;
            return new TrackedRecipe(parts[0],quality,amount,parts.Length==4 && parts[3]=="u");
        }
    }
}
