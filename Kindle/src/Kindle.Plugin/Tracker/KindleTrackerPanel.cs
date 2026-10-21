// Kindle - Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Presentation built on Crucible.UI factories; OpenKeep UI source is not copied.
// Reference behavior: OpenKeep 3.0.0 ad278ecf323901c3645eb1f9f6fac6833a0f8cb1,
// OpenKeep/OpenKeep/src/Tracker/{TrackerEntry,TrackerHud,TrackerVisibility}.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace ExtraPotions.Kindle.Runtime
{
    internal sealed class TrackerPanel:IDisposable
    {
        private readonly KitHudPanel hud;
        private readonly Transform owner;
        private readonly TMP_Text title;
        private readonly RectTransform list;
        private readonly List<Entry> entries=new List<Entry>();
        private bool interactive;
        private TrackerPanel(KitHudPanel hud,Transform owner)
        {
            this.hud=hud;this.owner=owner;
            title=Text(hud.Content,"Kindle_trackerTitle",KindleText.Text("tracker_title"),TrackerStyle.Size*0.8f,TrackerStyle.Dim);
            hud.SetInteractive(title,false);
            list=Node(hud.Content,"Kindle_trackerEntries");
        }
        internal static TrackerPanel? Create(Transform root)
        {
            var made=KitHudPanel.Create(root,"Kindle_tracker",new Vector2(300,60),new Vector2(20,330), global::ExtraPotions.Kindle.Runtime.Plugin.Guid, "Kindle_tracker");
            return made==null ? null : new TrackerPanel(made,root);
        }
        internal bool BelongsTo(Transform? root)=>hud.Root && ReferenceEquals(owner,root);
        internal void Update(bool show,bool free)
        {
            hud.Update(show,free,1f,KindleTracker.Settings.Get(KindleSettings.Tracker.BackgroundOpacity));
            if(interactive==free)return;interactive=free;Layout();
        }
        internal void Rebuild(IReadOnlyList<TrackedRecipe> tracked)
        {
            for(int i=list.childCount-1;i>=0;i--){var old=list.GetChild(i).gameObject;old.SetActive(false);UnityEngine.Object.Destroy(old);}
            entries.Clear();title.font=TrackerStyle.Font;title.fontSize=TrackerStyle.Size*0.8f;title.text=KindleText.Text("tracker_title");
            for(int i=0;i<tracked.Count;i++)if(tracked[i].Recipe)entries.Add(new Entry(this,list,tracked[i],i));
            Layout();
        }
        internal void Count(){foreach(var entry in entries)entry.Count();Layout();}
        private void Layout()
        {
            float width=Math.Max(title.preferredWidth,entries.Count==0 ? 100 : entries.Max(entry=>entry.Width(interactive)));
            width+=12f;float y=6f;PlaceRect((RectTransform)title.transform,6,y,width-12,TrackerStyle.Size);y+=TrackerStyle.Size+4;
            PlaceRect(list,6,y,width-12,1);float contentY=0;
            foreach(var entry in entries)
            {
                if(contentY>0){Divider.Place(list,new Vector2(0,-contentY),width-12,true,"Kindle_divider_"+entry.Index);contentY+=6;}
                contentY+=entry.Layout(contentY,width-12,interactive)+4;
            }
            list.sizeDelta=new Vector2(width-12,contentY);hud.Root.sizeDelta=new Vector2(width,y+contentY+6);
        }
        public void Dispose()=>hud.Dispose();
        private static RectTransform Node(Transform parent,string name)
        {
            var node=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)node.transform;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);return rect;
        }
        private static TMP_Text Text(Transform parent,string name,string text,float size,Color color)
        {
            var label=KitCanvas.Text(parent,name,text,size,color,TextAlignmentOptions.MidlineLeft,false);
            label.font=TrackerStyle.Font;return label;
        }
        private static void PlaceRect(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
        private static Image Icon(Transform parent,Sprite sprite,string name)
        {
            var rect=Node(parent,name);var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        private sealed class Entry
        {
            internal readonly int Index;
            private readonly TrackedRecipe tracked;
            private readonly Recipe recipe;
            private readonly RectTransform root,header;
            private readonly Image icon;
            private readonly TMP_Text name,amount;
            private readonly List<TMP_Text> notes=new List<TMP_Text>();
            private readonly List<Material> materials=new List<Material>();
            private readonly KitIconButton remove,less,more;
            private readonly TrackerPanel panel;
            internal Entry(TrackerPanel panel,Transform parent,TrackedRecipe tracked,int index)
            {
                this.panel=panel;this.tracked=tracked;recipe=tracked.Recipe!;Index=index;
                float size=TrackerStyle.Size;root=Node(parent,"Kindle_trackerEntry");header=Node(root,"header");
                var hit=header.gameObject.AddComponent<Image>();hit.color=Color.clear;panel.hud.SetInteractive(hit,true);
                KitWheel.Add(header.gameObject,direction=>KindleTracker.Step(index,direction,MeadworksInput.ShiftHeld));
                remove=KitIconButton.Create(header,"remove",KitGlyph.Cross,Vector2.zero,size*0.85f,_=>KindleTracker.Remove(index));
                less=KitIconButton.Create(header,"less",KitGlyph.Minus,Vector2.zero,size,_=>KindleTracker.Step(index,-1,MeadworksInput.ShiftHeld));
                more=KitIconButton.Create(header,"more",KitGlyph.Plus,Vector2.zero,size,_=>KindleTracker.Step(index,1,MeadworksInput.ShiftHeld));
                remove.Tooltip(KindleText.Text("tracker_untrack"),KindleTracker.DisplayName(recipe,tracked.Quality));
                less.Tooltip(KindleText.Text("tracker_track"),"−");more.Tooltip(KindleText.Text("tracker_track"),"+");
                foreach(var control in new[]{less,more})control.Wheel(direction=>KindleTracker.Step(index,direction,MeadworksInput.ShiftHeld));
                icon=Icon(header,recipe.m_item.m_itemData.GetIcon(),"icon");
                name=Text(header,"name",KindleTracker.DisplayName(recipe,tracked.Quality),size,TrackerStyle.Have);
                amount=Text(header,"amount","x"+tracked.Amount,size,TrackerStyle.Have);
                var station=recipe.GetRequiredStation(tracked.Quality);
                if(station)notes.Add(Text(root,"station",KindleText.Format("tracker_station",Localization.instance.Localize(station.m_name),recipe.GetRequiredStationLevel(tracked.Quality)),size*0.8f,TrackerStyle.Dim));
                if(recipe.m_requireOnlyOneIngredient)notes.Add(Text(root,"anyone",KindleText.Text("tracker_anyone"),size*0.8f,TrackerStyle.Dim));
                foreach(var requirement in recipe.m_resources.Where(requirement=>TrackerCounts.Shows(requirement,tracked)))
                {
                    var row=Node(root,"material");materials.Add(new Material{Root=row,Requirement=requirement,
                        Icon=Icon(row,requirement.m_resItem.m_itemData.GetIcon(),"icon"),
                        Label=Text(row,"name",Localization.instance.Localize(requirement.m_resItem.m_itemData.m_shared.m_name),size*0.9f,TrackerStyle.Have),
                        Count=Text(row,"count","",size*0.9f,TrackerStyle.Have)});
                }
            }
            internal float Width(bool open)
            {
                float size=TrackerStyle.Size,headerWidth=size*1.6f+3+name.preferredWidth;
                if(open || tracked.Amount>1)headerWidth+=size*1.6f+3;
                if(open)headerWidth+=size*2.85f+9;
                return Math.Max(headerWidth,Math.Max(notes.Count==0 ? 0 : notes.Max(note=>note.preferredWidth)+size*1.6f+3,
                    materials.Count==0 ? 0 : materials.Max(material=>material.Label.preferredWidth+size*4.65f+12)));
            }
            internal float Layout(float y,float width,bool open)
            {
                float size=TrackerStyle.Size;float x=0;
                foreach(var control in new[]{remove,less,more})control.Button.gameObject.SetActive(open);
                amount.gameObject.SetActive(open || tracked.Amount>1);
                if(open){PlaceRect((RectTransform)remove.Button.transform,x,0,size*0.85f,size*0.85f);x+=size*0.85f+3;}
                PlaceRect((RectTransform)icon.transform,x,0,size*1.6f,size*1.6f);x+=size*1.6f+3;
                float amountWidth=(open ? size*2+6 : 0)+(open || tracked.Amount>1 ? size*1.6f+3 : 0);
                PlaceRect((RectTransform)name.transform,x,0,width-x-amountWidth,size*1.6f);
                x=width-amountWidth;
                if(open){PlaceRect((RectTransform)less.Button.transform,x,0,size,size);x+=size+3;}
                PlaceRect((RectTransform)amount.transform,x,0,size*1.6f,size*1.6f);x+=size*1.6f+3;
                if(open)PlaceRect((RectTransform)more.Button.transform,x,0,size,size);
                PlaceRect(header,0,0,width,size*1.6f);float rowY=size*1.6f+1;
                foreach(var note in notes){PlaceRect((RectTransform)note.transform,size*1.6f+3,rowY,width-size*1.6f-3,size);rowY+=size+1;}
                foreach(var material in materials)
                {
                    PlaceRect(material.Root,0,rowY,width,size*1.25f);float indent=size*0.4f;
                    PlaceRect((RectTransform)material.Icon.transform,indent,0,size*1.25f,size*1.25f);indent+=size*1.25f+6;
                    PlaceRect((RectTransform)material.Label.transform,indent,0,width-indent-size*3-6,size*1.25f);
                    PlaceRect((RectTransform)material.Count.transform,width-size*3,0,size*3,size*1.25f);
                    material.Count.alignment=TextAlignmentOptions.MidlineRight;rowY+=size*1.25f+1;
                }
                PlaceRect(root,0,y,width,rowY);return rowY;
            }
            internal void Count()
            {
                bool all=true,any=false;
                foreach(var material in materials)
                {
                    int have=TrackerCounts.Have(material.Requirement),need=TrackerCounts.Need(material.Requirement,tracked);bool enough=have>=need;
                    material.Count.text=have+"/"+need;material.Count.color=material.Label.color=enough ? TrackerStyle.Have : TrackerStyle.Missing;
                    all&=enough;any|=enough;
                }
                name.color=(recipe.m_requireOnlyOneIngredient ? any : all) ? TrackerStyle.Ready : TrackerStyle.Have;
                amount.color=TrackerStyle.Have;amount.text="x"+tracked.Amount;
            }
        }
        private sealed class Material
        {
            internal RectTransform Root=null!;
            internal Piece.Requirement Requirement=null!;
            internal Image Icon=null!;
            internal TMP_Text Label=null!,Count=null!;
        }
    }
}
