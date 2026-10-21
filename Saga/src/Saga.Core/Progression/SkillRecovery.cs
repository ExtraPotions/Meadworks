// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Independently implemented from the documented GrindstoneSkills 0.18 recovery behavior.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace ExtraPotions.Saga.Progression {
    public sealed class SkillRecovery {
        public const string Key="saga.best_levels", ImportKey="saga.imported.best_levels";
        readonly Dictionary<int,int> best=new Dictionary<int,int>();
        public static SkillRecovery Load(IDictionary<string,string> data,IEnumerable<KeyValuePair<int,float>> current,IReadOnlyDictionary<int,int> aliases){
            var next=new SkillRecovery();
            if(data.TryGetValue(Key,out var saved))next.Merge(saved);
            if(!data.ContainsKey(ImportKey)&&data.TryGetValue("grindstone_best_levels",out var source))next.Merge(source,aliases);
            foreach(var skill in current)next.Note(skill.Key,skill.Value);
            data[Key]=next.Save();data[ImportKey]="1";return next;
        }
        public int Best(int skill)=>best.TryGetValue(skill,out var value)?value:0;
        public static int Base(float level)=>float.IsNaN(level)||float.IsInfinity(level)?0:(int)Math.Max(0,Math.Min(100,level));
        public bool Note(int skill,float level){int value=Base(level);if(skill<=0||skill==999||value<=Best(skill))return false;best[skill]=value;return true;}
        public void Forget(int skill)=>best.Remove(skill);
        public void ForgetAll()=>best.Clear();
        public void Merge(string text,IReadOnlyDictionary<int,int>? aliases=null){
            if(text.Length>131072)throw new FormatException("Recovery history exceeds 128 KiB.");
            var pairs=text.Split(',');if(pairs.Length>4096)throw new FormatException("Recovery history exceeds 4096 entries.");
            foreach(var pair in pairs){var fields=pair.Split(':');if(fields.Length!=2||!int.TryParse(fields[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out var id)||!int.TryParse(fields[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out var level))continue;
                if(aliases!=null&&aliases.TryGetValue(id,out var mapped))id=mapped;Note(id,level);
            }
        }
        public string Save()=>string.Join(",",best.OrderBy(p=>p.Key).Select(p=>p.Key.ToString(CultureInfo.InvariantCulture)+":"+p.Value.ToString(CultureInfo.InvariantCulture)));
        public static bool IsWeapon(int skill)=>skill>=1&&skill<=11&&skill!=6||skill==14;
        public float Rate(int skill,float storedLevel,int highestWeapon,bool catchUp,bool weaponSwitch){int level=Base(storedLevel);return catchUp&&level<Best(skill)?2f:weaponSwitch&&IsWeapon(skill)&&level<highestWeapon?1.5f:1f;}
    }
    // Exactly one owned subscription for each currently available skill; foreign subscriptions are never touched.
    public sealed class RecoveryProviders:IDisposable {
        readonly Dictionary<int,IDisposable> owned=new Dictionary<int,IDisposable>();
        public void Reconcile(IEnumerable<int> skills,Func<int,IDisposable> register){
            var wanted=new HashSet<int>(skills.Where(id=>id>0&&id!=999));
            foreach(var id in owned.Keys.Where(id=>!wanted.Contains(id)).ToArray()){owned[id].Dispose();owned.Remove(id);}
            foreach(var id in wanted)if(!owned.ContainsKey(id))owned.Add(id,register(id));
        }
        public void Dispose(){foreach(var lease in owned.Values)lease.Dispose();owned.Clear();}
    }
}
