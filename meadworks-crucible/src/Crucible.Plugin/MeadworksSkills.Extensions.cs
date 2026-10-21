// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Content supplied by a skill's owning product; the consuming book chooses presentation.</summary>
    public sealed class SkillPageLine {
        public string Text { get; }
        public bool Heading { get; }
        public string Title { get; }
        public string Tooltip { get; }
        public SkillPageLine(string text,bool heading=false,string title="",string tooltip=""){
            Text=text??"";Heading=heading;Title=title??"";Tooltip=tooltip??"";
        }
    }

    public static partial class MeadworksSkills {
        static readonly object extensionsGate=new object();
        static readonly Dictionary<string,ExperienceProvider> experienceProviders=new Dictionary<string,ExperienceProvider>(StringComparer.Ordinal);
        static readonly Dictionary<string,PageProvider> pageProviders=new Dictionary<string,PageProvider>(StringComparer.Ordinal);
        static readonly Dictionary<Skills.SkillType,IconProvider> iconProviders=new Dictionary<Skills.SkillType,IconProvider>();
        static readonly Dictionary<string,PublishedSkillProvider> publishedProviders=new Dictionary<string,PublishedSkillProvider>(StringComparer.Ordinal);
        static bool raisingExtension;

        sealed class PublishedSkillProvider {
            internal string Owner="";
            internal Skills.SkillType Skill;
            internal string Key="";
        }

        sealed class ExperienceProvider {
            internal string Owner="";
            internal Skills.SkillType Skill;
            internal Func<Player,float,float> Scale=null!;
            internal bool Warned;
        }
        sealed class PageProvider {
            internal string Owner="";
            internal Skills.SkillType Skill;
            internal Func<Player,float,IReadOnlyList<SkillPageLine>> Write=null!;
            internal bool Warned;
        }
        sealed class IconWrite {
            internal WeakReference<Skills.SkillDef> Definition=null!;
            internal Sprite? Before;
            internal Sprite Written=null!;
        }
        sealed class IconProvider {
            internal string Owner="";
            internal Func<Sprite?> Load=null!;
            internal Sprite? Icon;
            internal bool Warned;
            internal readonly List<IconWrite> Writes=new List<IconWrite>();
            internal void Restore(){
                foreach(var write in Writes)
                    if(write.Definition.TryGetTarget(out var definition)&&definition.m_icon==write.Written)definition.m_icon=write.Before;
                Writes.Clear();
            }
        }
        sealed class SkillExtensionLease:IDisposable {
            Action? remove;
            internal SkillExtensionLease(Action remove){this.remove=remove;}
            public void Dispose()=>Interlocked.Exchange(ref remove,null)?.Invoke();
        }
        static string ExtensionKey(string owner,Skills.SkillType skill){
            if(string.IsNullOrWhiteSpace(owner)||owner.IndexOf('\0')>=0)throw new ArgumentException("Skill extension owner required.",nameof(owner));
            if(skill<=0)throw new ArgumentOutOfRangeException(nameof(skill));
            return owner+"\0"+(int)skill;
        }

        /// <summary>Publishes a vanilla skill through the existing owner-only ZDO publisher. Dispose with the product's other skill leases.</summary>
        public static IDisposable RegisterPublishedLevel(string owner,Skills.SkillType skill,string publishKey){
            ExtensionKey(owner,skill);
            if(string.IsNullOrWhiteSpace(publishKey)||publishKey.IndexOf('\0')>=0)throw new ArgumentException("Skill publish key required.",nameof(publishKey));
            if(!Enum.IsDefined(typeof(Skills.SkillType),skill))throw new ArgumentException("Use the custom skill registry for custom skills.",nameof(skill));
            var item=new PublishedSkillProvider{Owner=owner,Skill=skill,Key=publishKey};
            lock(extensionsGate){
                if(publishedProviders.ContainsKey(publishKey)||All.Any(s=>s.Definition.PublishKey==publishKey))throw new InvalidOperationException("Skill publish key already registered: "+publishKey);
                publishedProviders.Add(publishKey,item);
            }
            return new SkillExtensionLease(()=>{lock(extensionsGate){if(publishedProviders.TryGetValue(publishKey,out var current)&&ReferenceEquals(current,item))publishedProviders.Remove(publishKey);}});
        }

        static bool HasPublishedLevels(){lock(skills)if(skills.Count>0)return true;lock(extensionsGate)return publishedProviders.Count>0;}

        static KeyValuePair<string,float>[] PublishedGameLevels(Player player){
            PublishedSkillProvider[] providers;
            lock(extensionsGate)providers=publishedProviders.Values.ToArray();
            return providers.Select(p=>new KeyValuePair<string,float>(p.Key,Modifiers.EffectiveLevel(SkillId.FromRaw((int)p.Skill),player.GetSkillLevel(p.Skill)))).ToArray();
        }

        static bool TryPublishedGameLevel(Player player,Skills.SkillType skill,out float level){
            string? key;
            lock(extensionsGate)key=publishedProviders.Values.Where(p=>p.Skill==skill).OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>p.Key).FirstOrDefault();
            level=0f;
            if(key==null)return false;
            var view=player.GetComponent<ZNetView>();
            if(view!=null&&view.IsValid())level=PublishedLevel.Sanitize(view.GetZDO().GetFloat(key,0f));
            return true;
        }

        /// <summary>Scales actual local-player experience inside the existing single RaiseSkill hook. Owners retain their native interaction scopes.</summary>
        public static IDisposable RegisterExperience(string owner,Skills.SkillType skill,Func<Player,float,float> scale){
            string key=ExtensionKey(owner,skill);
            var item=new ExperienceProvider{Owner=owner,Skill=skill,Scale=scale??throw new ArgumentNullException(nameof(scale))};
            lock(extensionsGate){if(experienceProviders.ContainsKey(key))throw new InvalidOperationException("Experience provider already registered: "+owner);experienceProviders.Add(key,item);}
            return new SkillExtensionLease(()=>{lock(extensionsGate){if(experienceProviders.TryGetValue(key,out var current)&&ReferenceEquals(current,item))experienceProviders.Remove(key);}});
        }

        static float ApplyRegisteredExperience(Skills target,Skills.SkillType skill,float factor){
            var player=Player.m_localPlayer;
            if(raisingExtension||player==null||SkillsPlayer(target)!=player)return factor;
            ExperienceProvider[] providers;
            lock(extensionsGate){if(experienceProviders.Count==0)return factor;providers=experienceProviders.Values.Where(p=>p.Skill==skill).OrderBy(p=>p.Owner,StringComparer.Ordinal).ToArray();}
            raisingExtension=true;
            try{
                foreach(var provider in providers){
                    lock(extensionsGate)if(!experienceProviders.Values.Contains(provider))continue;
                    try{
                        float next=provider.Scale(player,factor);
                        if(float.IsNaN(next)||float.IsInfinity(next)||next<0f)throw new InvalidOperationException("Invalid experience factor.");
                        factor=next;
                    }catch(Exception error){if(!provider.Warned){provider.Warned=true;Log?.Warning("Skill experience provider "+provider.Owner+" failed: "+error.GetType().Name);}}
                }
            }finally{raisingExtension=false;}
            return factor;
        }

        /// <summary>Registers skill content for Saga or another consumer without adding a skill book or a UI dependency.</summary>
        public static IDisposable RegisterPage(string owner,Skills.SkillType skill,Func<Player,float,IReadOnlyList<SkillPageLine>> write){
            string key=ExtensionKey(owner,skill);
            var item=new PageProvider{Owner=owner,Skill=skill,Write=write??throw new ArgumentNullException(nameof(write))};
            lock(extensionsGate){if(pageProviders.ContainsKey(key))throw new InvalidOperationException("Skill page provider already registered: "+owner);pageProviders.Add(key,item);}
            return new SkillExtensionLease(()=>{lock(extensionsGate){if(pageProviders.TryGetValue(key,out var current)&&ReferenceEquals(current,item))pageProviders.Remove(key);}});
        }

        public static IReadOnlyList<SkillPageLine> PagesFor(Player? player,Skills.SkillType skill){
            if(player==null)return Array.Empty<SkillPageLine>();
            PageProvider[] providers;
            lock(extensionsGate)providers=pageProviders.Values.Where(p=>p.Skill==skill).OrderBy(p=>p.Owner,StringComparer.Ordinal).ToArray();
            var lines=new List<SkillPageLine>();
            float level=EffectiveLevel(player,skill);
            foreach(var provider in providers){
                lock(extensionsGate)if(!pageProviders.Values.Contains(provider))continue;
                try{var page=provider.Write(player,level);if(page!=null)lines.AddRange(page.Where(line=>line!=null));}
                catch(Exception error){if(!provider.Warned){provider.Warned=true;Log?.Warning("Skill page provider "+provider.Owner+" failed: "+error.GetType().Name);}}
            }
            return lines.ToArray();
        }

        /// <summary>Registers one temporary icon owner for a vanilla skill. Disposal restores only definitions still holding that owner's icon.</summary>
        public static IDisposable RegisterIcon(string owner,Skills.SkillType skill,Func<Sprite?> load){
            ExtensionKey(owner,skill);
            var item=new IconProvider{Owner=owner,Load=load??throw new ArgumentNullException(nameof(load))};
            lock(extensionsGate){if(iconProviders.ContainsKey(skill))throw new InvalidOperationException("Skill icon already has an owner: "+skill);iconProviders.Add(skill,item);}
            ApplyRegisteredIcons(Player.m_localPlayer?.GetSkills());
            return new SkillExtensionLease(()=>{lock(extensionsGate){if(iconProviders.TryGetValue(skill,out var current)&&ReferenceEquals(current,item)){iconProviders.Remove(skill);item.Restore();}}});
        }

        static void ApplyRegisteredIcons(Skills? target){
            if(target==null||target.m_skills==null||MeadworksHeadless.IsHeadless)return;
            KeyValuePair<Skills.SkillType,IconProvider>[] providers;
            lock(extensionsGate)providers=iconProviders.ToArray();
            foreach(var pair in providers){
                var provider=pair.Value;
                lock(extensionsGate)if(!iconProviders.TryGetValue(pair.Key,out var current)||!ReferenceEquals(current,provider))continue;
                try{if(provider.Icon==null)provider.Icon=provider.Load();}
                catch(Exception error){if(!provider.Warned){provider.Warned=true;Log?.Warning("Skill icon provider "+provider.Owner+" failed: "+error.GetType().Name);}continue;}
                if(provider.Icon==null)continue;
                provider.Writes.RemoveAll(write=>!write.Definition.TryGetTarget(out _));
                foreach(var definition in target.m_skills.Where(d=>d!=null&&d.m_skill==pair.Key)){
                    if(provider.Writes.Any(w=>w.Definition.TryGetTarget(out var d)&&ReferenceEquals(d,definition)))continue;
                    provider.Writes.Add(new IconWrite{Definition=new WeakReference<Skills.SkillDef>(definition),Before=definition.m_icon,Written=provider.Icon});
                    definition.m_icon=provider.Icon;
                }
            }
        }

        static void ResetExtensions(){lock(extensionsGate){foreach(var icon in iconProviders.Values)icon.Restore();iconProviders.Clear();pageProviders.Clear();experienceProviders.Clear();publishedProviders.Clear();raisingExtension=false;}}
    }
}
