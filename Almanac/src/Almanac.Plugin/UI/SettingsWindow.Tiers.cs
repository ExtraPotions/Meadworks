// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Product pages exclude File-only rows; Advanced and search reveal specialist settings.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        bool essentialView;
        readonly HashSet<string> serverExpanded=new HashSet<string>(StringComparer.Ordinal);
        RowModel[] shownRows=Array.Empty<RowModel>();

        /// <summary>True while the window remains scoped to one mod's MeadWorks menu page.</summary>
        public bool EssentialView=>essentialView;

        /// <summary>Leaves the mod's page for every setting of the same mod.</summary>
        public void ShowAllSettings()
        {
            if(!essentialView)return;
            essentialView=false;lastFingerprint="";dirty=true;
        }

        /// <summary>Folds or unfolds one mod's "Set by the server" rows.</summary>
        public void ToggleServerRows(string group)
        {
            if(!serverExpanded.Remove(group))serverExpanded.Add(group);
            lastFingerprint="";dirty=true;
        }

        /// <summary>The right-hand rows as last drawn: kind, group-free key and title, for native checks.</summary>
        public IReadOnlyList<string> VisibleRows()=>shownRows.Select(row=>
            (row.Preset!=null?"preset":row.Action.Length>0?row.Action:row.FeatureId.Length>0?"feature":row.Header||row.WorldHeading?"heading":row.World!=null?"world":row.Native!=null?(row.ServerSet?"server-row":row.FileOnly?"file-only":"setting"):"info")
            +"|"+(row.Native!=null?row.Native.Entry.Definition.Section+"/"+row.Native.Entry.Definition.Key:row.Key)+"|"+row.Title).ToArray();

        /// <summary>Bound "Set by the server" rows as "key|usable control names" (empty when read-only), for native checks.</summary>
        public IReadOnlyList<string> ServerRowControls()=>views.Values.Where(view=>view.ServerSet)
            .Select(view=>view.RowKey+"|"+string.Join(",",view.UsableControls())).ToArray();

        string TierFingerprint()
        {
            var session=CruciblePlugin.IsReady?CruciblePlugin.Session:null;
            return essentialView+"|"+string.Join(",",serverExpanded.OrderBy(key=>key,StringComparer.Ordinal))+"|"+session?.Role+"|"+session?.IsAdmin+"|"+session?.AuthorityReady+"|"+session?.Generation;
        }

        bool InProductScope(IndexGroup group)=>productFilter.Length==0||group.Key==productFilter||SettingTierRules.Companion(productFilter,group.Key);

        static SettingTier Tier(IndexGroup group,ConfigEntryBase entry)
        {
            try{return SettingTiers.Tier(group.Key,entry.Definition.Section,entry.Definition.Key);}
            catch{return SettingTier.Advanced;}
        }

        static bool UsesTiers(string product)=>product.StartsWith("com.meadworks.",StringComparison.Ordinal)||SettingTiers.For(product).Declared;

        bool TierShows(SettingTier tier,NativeEntry native,IndexedFile? file,bool match,string? product=null)
        {
            return SettingsVisibility.Shows(tier,UsesTiers(product??file?.GroupKey??""),native.Attributes,
                essentialView,showAdvanced,Searching&&match,file?.AllEntriesAdvanced==true);
        }

        static bool ServerFolds(NativeWriteFacts facts)
        {
            if(facts==null||!facts.Known||!CruciblePlugin.IsReady)return false;
            var session=CruciblePlugin.Session;
            return SettingTierRules.CollapseHostOwned(session.Role,session.IsAdmin,facts.DeclaredScope);
        }

        RowModel AllSettingsRow(IndexGroup group)=>new RowModel{Key="all-settings:"+group.Key,Action="all-settings",
            Title=text("All settings in Almanac"),Info=text("This page lists the everyday settings. Open every setting of this mod in Almanac.")};

        void AddServerRows(IndexGroup group,List<RowModel> rows,List<RowModel> folded,int at)
        {
            if(folded.Count==0)return;
            bool open=serverExpanded.Contains(group.Key);
            var line=new List<RowModel>{new RowModel{Key="server:"+group.Key,Action="server",Section=group.Key,
                Title=(open?"- ":"+ ")+string.Format(text("Set by the server ({0} settings)"),folded.Count),
                Info=text("The server decides these settings while you are connected. Click to show them read-only; your own values return when you play alone.")}};
            if(open)line.AddRange(folded);
            rows.InsertRange(Mathf.Clamp(at,0,rows.Count),line);
        }

        /// <summary>On a mod's page, companion plugins (Crucible UI on the Crucible page) join the mod's own rows.</summary>
        void MergeCompanions(List<(IndexGroup Group,List<RowModel> Rows)> groups)
        {
            if(!essentialView||productFilter.Length==0)return;
            int primary=groups.FindIndex(item=>item.Group.Key==productFilter);
            if(primary<0)return;
            foreach(var companion in groups.Where(item=>SettingTierRules.Companion(productFilter,item.Group.Key)).ToArray())
            {
                groups[primary].Rows.AddRange(companion.Rows);
                groups.Remove(companion);
            }
            selected=productFilter;
        }

        void BindTierAction(RectTransform rect,RowModel row)
        {
            UnbindFeature(rect);
            Action act=row.Action=="all-settings"?(Action)ShowAllSettings:()=>ToggleServerRows(row.Section);
            var button=AlmanacButtons.Create(rect,row.Action=="all-settings"?"AllSettings":"ServerSettings",row.Title,act,Vector2.zero,new Vector2(Mathf.Max(100,rect.rect.width-16),32));
            Help(button.gameObject,row.Title,row.Info);
            Label(rect,"ActionDescription",row.Info,new Vector2(8,-36),new Vector2(Mathf.Max(100,rect.rect.width-16),36),14);
            window!.Focusable(button,1,row.Index+2);
        }
    }
}
