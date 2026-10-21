// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Attributes;
using ExtraPotions.Almanac.Editors;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Almanac.World;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Almanac.Runtime.UI
{
    public sealed partial class SettingsWindow
    {
        readonly Dictionary<RectTransform,WorldRowView> worldViews=new Dictionary<RectTransform,WorldRowView>();
        readonly Dictionary<string,(WorldEditWatch Watch,string Error)> worldWrites=new Dictionary<string,(WorldEditWatch,string)>();
        void TickWorldEdits()
        {
            var snapshot=MeadworksWorldSettings.Snapshot();
            foreach(var edit in worldWrites)
            {
                var live=snapshot.FirstOrDefault(row=>edit.Key.StartsWith("world:"+row.Key+"|",StringComparison.Ordinal));
                edit.Value.Watch.Observe(live?.WorldId??0,live?.Generation??0,live?.WorldText,live?.Decision.Allowed==true);
            }
        }
        void AddWorldRows(IndexGroup group,List<RowModel> rows,StringBuilder fingerprint)
        {
            var worldRows=new List<RowModel>();
            foreach(var metadata in MeadworksWorldSettings.Snapshot().Where(row=>row.ProductId==group.Key))
            {
                var native=new NativeEntry(metadata.Config);
                if(Hidden(metadata.Config)||native.Attributes.Browsable==false)continue;
                string reason=WorldReason(metadata,native);
                var display=EntryPresentation.Resolve(native,CrucibleUiPlugin.Language,text);
                string title=display.Section+" / "+display.Label;
                string value=WorldValue(metadata);
                bool match=Matches(group.DisplayName+" World "+metadata.Config.Definition+" "+title+" "+value+" "+reason);
                var tier=Tier(group,metadata.Config);
                if(!match||(essentialView?!SettingTierRules.ShowOnProductPage(tier)
                    :Presentation.Decide(native.Attributes,showAdvanced,Searching&&match,false)!=Visibility.Visible||!SettingTierRules.ShowInAlmanac(tier,showAdvanced)))continue;
                if(filters.Any(filter=>filter!="meadworks"&&filter!="synced"&&!(filter=="changed"&&metadata.WorldText!=null)&&!(filter=="locked"&&reason.Length>0)))continue;
                string key=WorldRowKey(metadata);
                worldRows.Add(new RowModel{Key=key,Title=title,Info=display.Help,World=metadata});
                fingerprint.Append(key).Append(value).Append(reason).Append(WorldStatus(metadata))
                    .Append(display.Label).Append(display.Section).Append(display.Help);
            }
            if(worldRows.Count==0)return;
            rows.Add(new RowModel{Key="world-heading:"+group.Key,Title=text("World"),Info=text("World overrides travel with this save. Config remains the server fallback."),WorldHeading=true});
            rows.AddRange(worldRows);
        }
        static string WorldRowKey(WorldSettingMetadata metadata)=>"world:"+metadata.Key+"|"+metadata.WorldId+"|"+metadata.Generation;
        static string WorldValue(WorldSettingMetadata metadata)=>metadata.WorldText??(metadata.Config.BoxedValue is IFormattable value
            ?value.ToString(null,CultureInfo.InvariantCulture):Convert.ToString(metadata.Config.BoxedValue,CultureInfo.InvariantCulture)??"");
        WorldSettingMetadata? LiveWorldRow(WorldSettingMetadata shown)=>MeadworksWorldSettings.Snapshot().FirstOrDefault(row=>row.Key==shown.Key&&ReferenceEquals(row.Config,shown.Config));
        string WorldReason(WorldSettingMetadata metadata,NativeEntry native)
        {
            if(Hidden(metadata.Config)||native.Attributes.Browsable==false||!discovery.IsPluginBrowsable(metadata.ProductId))return text("Hidden by server policy.");
            if(native.Tags.Errors.Count>0)return string.Join("; ",native.Tags.Errors);
            if(native.Attributes.ReadOnly==true||native.Attributes.CharterReadOnly||native.Attributes.GenericReadOnly)return text("The mod marks this setting read-only.");
            return metadata.Decision.Allowed?"":MeadworksBlockedOptions.Reason(metadata.Decision.Reason);
        }
        string WorldStatus(WorldSettingMetadata metadata)
        {
            if(!worldWrites.TryGetValue(WorldRowKey(metadata),out var state))return "";
            state.Watch.Observe(metadata.WorldId,metadata.Generation,metadata.WorldText,metadata.Decision.Allowed);
            return state.Error.Length>0?state.Error:state.Watch.Stale?text("The world or session changed; reopen this edit."):
                state.Watch.Pending?text("Queued; waiting for the world value."):text("Applied");
        }
        string WorldInputError(WorldSettingMetadata metadata,string input)
        {
            var native=new NativeEntry(metadata.Config);
            return native.Parse(metadata.Config.SettingType==typeof(string)?TomlTypeConverter.ConvertToString(input,typeof(string)):input).Error??"";
        }
        void WriteWorld(WorldSettingMetadata shown,string? input)
        {
            var live=LiveWorldRow(shown);
            if(live==null||live.WorldId!=shown.WorldId||live.Generation!=shown.Generation)
            {SetActionText("The world or session changed; reopen this edit.");dirty=true;return;}
            var native=new NativeEntry(shown.Config);string reason=WorldReason(live,native);
            if(reason.Length>0){SetActionLiteral(reason);dirty=true;return;}
            if(worldWrites.TryGetValue(WorldRowKey(shown),out var outstanding)&&outstanding.Error.Length==0&&outstanding.Watch.Pending)
            {SetActionText("Queued; waiting for the world value.");dirty=true;return;}
            string? requested=null;
            if(input!=null)
            {
                var parsed=native.Parse(shown.Config.SettingType==typeof(string)?TomlTypeConverter.ConvertToString(input,typeof(string)):input);
                if(!parsed.Valid){SetActionLiteral(parsed.Error??text("Invalid value"));dirty=true;return;}
                requested=parsed.Value is IFormattable formatted?formatted.ToString(null,CultureInfo.InvariantCulture):Convert.ToString(parsed.Value,CultureInfo.InvariantCulture)??"";
            }
            // The displayed metadata owns the exact registered binding and world/session stamp. Never assign the cfg entry.
            var result=input==null?shown.Clear():shown.Set(requested!);
            bool admitted=result.Status==WorldSettingWriteStatus.Applied||result.Status==WorldSettingWriteStatus.Queued;
            string error=admitted?"":result.Status==WorldSettingWriteStatus.Refused?MeadworksBlockedOptions.Reason(result.Decision.Reason):(result.Status==WorldSettingWriteStatus.Invalid?text("Invalid world value"):text("World editing is unavailable; refresh this view."));
            worldWrites[WorldRowKey(shown)]=(new WorldEditWatch(shown.WorldId,shown.Generation,requested,result.Applied),error);
            if(admitted)SetActionText(result.Applied?"Applied":"Queued; waiting for the world value.");else SetActionLiteral(error);
            dirty=true;
        }
        void BindWorld(RectTransform rect,RowModel row)
        {
            if(!worldViews.TryGetValue(rect,out var view)||!view.Matches(row))
            {UnbindWorld(rect);worldViews[rect]=view=new WorldRowView(this,rect,row);}
            view.Refresh();
        }
        void UnbindWorld(RectTransform rect)
        {if(worldViews.TryGetValue(rect,out var view)){view.Dispose();worldViews.Remove(rect);}Clear(rect);}
        void ReleaseWorldUi(){foreach(var view in worldViews.Values)view.Dispose();worldViews.Clear();}
        sealed class WorldRowView:IDisposable
        {
            readonly SettingsWindow owner;readonly WorldSettingMetadata shown;readonly RectTransform rect;
            readonly TMP_Text label,detail,status;readonly Button clear;Button? edit;KitToggle? toggle;KitAlmanacNumber? number;
            string presentationLanguage="";
            public string Key{get;}
            internal bool Matches(RowModel row)=>Key==row.Key&&ReferenceEquals(shown.Config,row.World?.Config);
            internal bool Contains(Transform selected)=>selected.IsChildOf(rect);
            internal void Nudge(int amount)=>number?.Step(amount);
            internal bool Activate()
            {
                if(number!=null&&EventSystem.current?.currentSelectedGameObject==number.Field.Input.gameObject){number.Field.Focus();return true;}
                return false;
            }
            internal WorldRowView(SettingsWindow owner,RectTransform rect,RowModel row)
            {
                this.owner=owner;this.rect=rect;shown=row.World!;Key=row.Key;
                label=Label(rect,"Name",row.Title,Vector2.zero,Vector2.zero,16);
                detail=Label(rect,"Source","",Vector2.zero,Vector2.zero,14);detail.raycastTarget=true;
                status=Label(rect,"State","",Vector2.zero,Vector2.zero,14);status.raycastTarget=true;
                var native=new NativeEntry(shown.Config);
                var numeric=EditorSelector.Fallback(native.Shape,native.Attributes,WorldValue(shown));
                double increment=numeric.Step??1;
                if(shown.Config.SettingType==typeof(bool))toggle=KitToggle.Create(rect,"WorldValue",bool.TryParse(WorldValue(shown),out bool on)&&on,value=>owner.WriteWorld(shown,value.ToString()),Vector2.zero,new Vector2(150,30),owner.text("On"),owner.text("Off"));
                else if(native.Shape.Kind==SettingKind.Integer||native.Shape.Kind==SettingKind.Floating)
                    number=KitAlmanacNumber.Create(rect,"WorldValue",Vector2.zero,new Vector2(180,30),WorldValue(shown),value=>owner.WorldInputError(shown,value).Length==0,value=>owner.WriteWorld(shown,value),direction=>
                    {
                        var live=owner.LiveWorldRow(shown)??shown;
                        double current=double.Parse(WorldValue(live),CultureInfo.InvariantCulture);
                        return TomlTypeConverter.ConvertToString(Convert.ChangeType(current+direction*increment,shown.Config.SettingType,CultureInfo.InvariantCulture),shown.Config.SettingType);
                    },
                        native.Shape.HasRange?native.Shape.RangeMin:(double?)null,native.Shape.HasRange?native.Shape.RangeMax:(double?)null,increment,numeric.Percent,
                        value=>TomlTypeConverter.ConvertToString(Convert.ChangeType(value,shown.Config.SettingType,CultureInfo.InvariantCulture),shown.Config.SettingType));
                else edit=AlmanacButtons.Create(rect,"WorldValue",WorldValue(shown),()=>
                {
                    var live=owner.LiveWorldRow(shown);if(live==null)return;
                    var options=native.Shape.AcceptableList??native.Shape.EnumNames;
                    if(options.Count>0)KitAlmanacChoice.Open(owner.window!,label.text,options,new[]{WorldValue(live)},false,chosen=>owner.WriteWorld(shown,chosen[0]));
                    else KitAlmanacTextEditor.Open(owner.window!,label.text,WorldValue(live),value=>owner.WorldInputError(shown,value),value=>owner.WriteWorld(shown,value),owner.WorldReason(live,new NativeEntry(shown.Config)).Length>0);
                },Vector2.zero,new Vector2(180,30));
                clear=AlmanacButtons.Create(rect,"ClearWorld",owner.text("Use Config"),()=>owner.WriteWorld(shown,null),Vector2.zero,new Vector2(110,30));
                if(toggle!=null)owner.window!.Focusable(toggle.Button,1,row.Index+2);
                if(number!=null){owner.window!.Focusable(number.Field.Input,1,row.Index+2);owner.window.Focusable(number.Less,2,row.Index+2);owner.window.Focusable(number.More,3,row.Index+2);if(number.Slider!=null)owner.window.Focusable(number.Slider,2,row.Index+2);}
                if(edit!=null)owner.window!.Focusable(edit,1,row.Index+2);owner.window!.Focusable(clear,4,row.Index+2);
            }
            internal void Refresh()
            {
                string language=CrucibleUiPlugin.Language;
                if(!string.Equals(presentationLanguage,language,StringComparison.Ordinal))
                {
                    presentationLanguage=language;owner.LocalizedCaption(clear,"Use Config");
                    toggle?.SetLabels(owner.text("On"),owner.text("Off"));
                }
                var live=owner.LiveWorldRow(shown);string reason=live==null?owner.text("World setting is no longer registered."):owner.WorldReason(live,new NativeEntry(shown.Config));
                var display=EntryPresentation.Resolve(new NativeEntry(shown.Config),CrucibleUiPlugin.Language,owner.text);
                label.text=display.Section+" / "+display.Label;
                if(live!=null&&(live.WorldId!=shown.WorldId||live.Generation!=shown.Generation))reason=owner.text("The world or session changed; reopen this edit.");
                bool can=reason.Length==0;
                if(owner.Hidden(shown.Config)){rect.gameObject.SetActive(false);owner.dirty=true;return;}
                float width=owner.rightWidth-20;bool stacked=owner.rightWidth<620;float x=stacked?8:Mathf.Max(240,width*.46f),y=stacked?-38:-3,valueWidth=Mathf.Max(90,width-x-120);
                Set(label.rectTransform,new Vector2(8,-4),new Vector2(stacked?width:x-20,30));
                if(toggle!=null){Set((RectTransform)toggle.Button.transform,new Vector2(x,y),new Vector2(valueWidth,30));toggle.SetUsable(can,reason);if(live!=null)toggle.SetValue(bool.TryParse(WorldValue(live),out bool on)&&on);}
                if(number!=null){number.Place(new Vector2(x,y),valueWidth);number.SetUsable(can);if(live!=null&&double.TryParse(WorldValue(live),NumberStyles.Float,CultureInfo.InvariantCulture,out var value))number.Set(WorldValue(live),value);}
                if(edit!=null){Set((RectTransform)edit.transform,new Vector2(x,y),new Vector2(valueWidth,30));edit.interactable=can;if(live!=null)edit.GetComponentInChildren<TMP_Text>().text=WorldValue(live);}
                Set((RectTransform)clear.transform,new Vector2(x+valueWidth+8,y),new Vector2(110,30));clear.interactable=can&&live?.WorldText!=null;
                float extra=number?.Slider!=null?40:0;
                Set(detail.rectTransform,new Vector2(8,(stacked?-76:-40)-extra),new Vector2(width,36));
                detail.text=owner.text("Source")+": "+(live==null?owner.text("Unavailable"):live.Source==WorldSettingSource.World?owner.text("World"):live.Source==WorldSettingSource.Config?owner.text("Config"):owner.text("Default"))+" · "+owner.text("Config fallback")+": "+new NativeEntry(shown.Config).Text();
                KitTooltip.Set(detail.gameObject,label.text,shown.Config.Definition+"\n"+display.Help+"\n"+detail.text);
                Set(status.rectTransform,new Vector2(8,(stacked?-116:-80)-extra),new Vector2(width,24));status.text=reason.Length>0?reason:live==null?"":owner.WorldStatus(live);
                KitTooltip.Set(status.gameObject,label.text,status.text);KitTooltip.Set(clear.gameObject,owner.text("Use Config"),owner.text("Remove only this world's override. The config file is unchanged."));
                string help=owner.SettingHelp(new NativeEntry(shown.Config),shown.Config.Description.Description,live==null?owner.text("Unavailable"):WorldValue(live),reason)+"\n"+detail.text;
                label.raycastTarget=true;owner.Help(label.gameObject,label.text,help);
                detail.raycastTarget=true;owner.Help(detail.gameObject,label.text,help);
                status.raycastTarget=true;owner.Help(status.gameObject,label.text,help);
                toggle?.Tooltip(label.text,help);
                if(edit!=null)owner.Help(edit.gameObject,label.text,help,owner.text("Change this world's override. The config fallback remains unchanged."));
                if(number!=null)
                {
                    owner.Help(number.Field.Input.gameObject,label.text,help);
                    owner.Help(number.Less.gameObject,label.text,help,owner.text("Decrease this value."));
                    owner.Help(number.More.gameObject,label.text,help,owner.text("Increase this value."));
                    if(number.Slider!=null)owner.Help(number.Slider.gameObject,label.text,help);
                }
                owner.Help(clear.gameObject,owner.text("Use Config"),help,owner.text("Remove only this world's override. The config file is unchanged."));
            }
            public void Dispose()
            {
                // Unity may already have destroyed the canvas during logout; release managed leases regardless.
                if(rect!=null)foreach(var control in rect.GetComponentsInChildren<Selectable>(true))owner.window?.Unfocusable(control);
                toggle?.Dispose();number?.Dispose();
            }
        }
    }
}
