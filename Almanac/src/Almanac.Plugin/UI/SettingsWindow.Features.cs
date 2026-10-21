// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        readonly MeadworksMenuReturn featureReturn=new MeadworksMenuReturn();
        MeadworksMenuReturn.Suspension? rosterReturn;
        KitWindow? featureParent;

        void AddFeatureRows(IndexGroup group,List<RowModel> rows,StringBuilder fingerprint)
        {
            const string prefix="com.meadworks.";
            if(!group.Key.StartsWith(prefix,StringComparison.Ordinal))return;
            string product=group.Key.Substring(prefix.Length);
            foreach(var action in MeadworksMenuProducts.Actions(product,MeadworksMenu.Registry.Choices()))
            {
                // Almanac's base callback opens this same settings book, not a separate feature.
                if(action.Id=="almanac")continue;
                string info=MeadworksMenuTips.ActionText(action.Id,CrucibleUiPlugin.Language);
                if(!Matches(group.DisplayName+" "+action.Caption+" "+info))continue;
                rows.Add(new RowModel{Key="feature:"+action.Id,FeatureId=action.Id,Title=action.Caption,Info=text("Mod")+": "+group.DisplayName+"\n"+info});
                fingerprint.Append(action.Id).Append(action.Caption).Append(info);
            }
        }

        void BindFeature(RectTransform rect,RowModel row)
        {
            UnbindFeature(rect);
            var button=AlmanacButtons.Create(rect,"Feature_"+row.FeatureId,row.Title,()=>OpenFeature(row.FeatureId),Vector2.zero,new Vector2(Mathf.Max(100,rect.rect.width-16),32));
            Help(button.gameObject,row.Title,row.Info);
            Label(rect,"FeatureDescription",row.Info,new Vector2(8,-36),new Vector2(Mathf.Max(100,rect.rect.width-16),36),14);
            window!.Focusable(button,1,row.Index+2);
        }
        void UnbindFeature(RectTransform rect)
        {
            if(window!=null)foreach(var control in rect.GetComponentsInChildren<Selectable>(true))window.Unfocusable(control);
            Clear(rect);
        }
        void OpenFeature(string id)
        {
            if(!IsOpen)return;
            CancelFeatureReturn();
            featureParent=window;
            rosterReturn=MeadworksPauseMenu.SuspendReturn();
            featureReturn.Chosen(Time.unscaledTime);
            bool opened=false;
            try
            {
                window!.Close();
                opened=MeadworksMenu.Registry.Open(id);
            }
            finally {if(!opened)RestoreFeatureParent();}
        }
        void TickFeatureReturn()
        {
            if(!featureReturn.Pending)return;
            if(disposed||window?.Panel==null||!ReferenceEquals(window,featureParent)||rosterReturn?.IsCurrent!=true){CancelFeatureReturn();return;}
            var next=featureReturn.Tick(Time.unscaledTime,KitWindows.AnyOpen);
            if(next==MeadworksMenuReturnAction.Reopen||next==MeadworksMenuReturnAction.Forget)RestoreFeatureParent();
        }
        void RestoreFeatureParent()
        {
            var lease=rosterReturn;rosterReturn=null;
            var parent=featureParent;featureParent=null;featureReturn.Clear();
            try
            {
                if(!disposed&&ReferenceEquals(parent,window)&&window?.Panel!=null&&lease?.IsCurrent==true)
                {
                    window.Open();
                    if(!disposed&&window?.IsOpen==true&&ReferenceEquals(parent,window))lease.Resume(Time.unscaledTime);
                }
            }
            finally {lease?.Dispose();}
        }
        void CancelFeatureReturn()
        {
            featureReturn.Clear();featureParent=null;
            rosterReturn?.Dispose();rosterReturn=null;
        }
    }
}
