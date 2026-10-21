// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// ProgressivePowers behavior and user YAML schema only. No unlicensed implementation or prose copied.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Saga.Conversion;
namespace ExtraPotions.Saga.Powers {
    public static class PowerEffects {
        public static IReadOnlyList<EffectValue> FromFeatures(Boss boss,int mastery,Func<PowerFeature,object?> value) => PowerCatalog.For(boss).Where(f=>f.Index<=mastery).SelectMany(f=>f.Effects(value(f))).ToArray();
        public static int EffectiveMastery(int earned,int? forced,int levels)=>Math.Min(Math.Min(100,Math.Max(0,levels)),Math.Max(0,forced??earned));
    }
    public sealed class PowerDefinitions {
        static readonly HashSet<string> Skills=new HashSet<string>(("Swords Knives Clubs Polearms Spears Blocking Axes Bows ElementalMagic BloodMagic Unarmed Pickaxes WoodCutting Crossbows Jump Sneak Run Swim Fishing Cooking Farming Crafting Dodge Ride All").Split(' '),StringComparer.Ordinal);
        static readonly HashSet<string> Damage=new HashSet<string>(("Blunt Slash Pierce Chop Pickaxe Fire Frost Lightning Poison Spirit Pure Melee Elemental All").Split(' '),StringComparer.Ordinal);
        readonly Dictionary<Boss,SortedDictionary<int,List<EffectValue>>> powers;
        PowerDefinitions(Dictionary<Boss,SortedDictionary<int,List<EffectValue>>> powers){this.powers=powers;}
        public bool Lists(Boss boss)=>powers.ContainsKey(boss);
        public int LevelCount(Boss boss)=>powers.TryGetValue(boss,out var levels)&&levels.Count>0?levels.Keys.Max():0;
        public IReadOnlyList<EffectValue> Effects(Boss boss,int mastery)=>powers.TryGetValue(boss,out var levels)?levels.Where(p=>p.Key<=mastery).SelectMany(p=>p.Value).ToArray():Array.Empty<EffectValue>();
        public static PowerDefinitions Parse(string? text,out List<string> errors) {
            errors=new List<string>(); var root=MiniYaml.Parse(text,errors); var powers=new Dictionary<Boss,SortedDictionary<int,List<EffectValue>>>();
            foreach(var power in root.Entries) {
                if(!PowerCatalog.TryParse(power.Key,out var boss)||power.Key!=PowerCatalog.Name(boss)){errors.Add("Unknown power: "+power.Key);continue;}
                var levels=new SortedDictionary<int,List<EffectValue>>(); powers[boss]=levels;
                var skillTargets=new Dictionary<PowerEffect,HashSet<string>>();
                foreach(var level in power.Value.Entries) {
                    if(!int.TryParse(level.Key,out var number)||number<1||number>100){errors.Add("Invalid level: "+level.Key);continue;}
                    var effects=new List<EffectValue>();levels[number]=effects;
                    foreach(var entry in level.Value.Entries) {
                        if(!Enum.TryParse(entry.Key,out PowerEffect effect)||!Enum.IsDefined(typeof(PowerEffect),effect)){errors.Add("Unknown effect: "+entry.Key);continue;}
                        if(effect==PowerEffect.HealthTick) {
                            bool hasHealth=false;
                            foreach(var item in entry.Value.Entries) {
                                if(item.Key!="Health"&&item.Key!="Interval"&&item.Key!="MinHealth"){errors.Add("Invalid health tick field: "+item.Key);continue;}
                                if(!Number(item.Value.Scalar,out var n)||item.Key=="Interval"&&n<=0||item.Key=="MinHealth"&&(n<0||n>1)){errors.Add("Invalid health tick value: "+item.Key);continue;}
                                if(item.Key=="Health"){hasHealth=true;if(n!=0)effects.Add(new EffectValue(effect,n));}
                                else if(item.Key=="Interval"?n!=10:n!=0)effects.Add(new EffectValue(effect,n,item.Key));
                            }
                            if(!hasHealth)errors.Add("HealthTick requires Health");
                        } else if(effect==PowerEffect.Resistance||effect==PowerEffect.DamageDealt||effect==PowerEffect.SkillLevel||effect==PowerEffect.SkillGain||effect==PowerEffect.AttackDamage) {
                            foreach(var item in entry.Value.Entries) {
                                bool skill=effect==PowerEffect.SkillLevel||effect==PowerEffect.SkillGain||effect==PowerEffect.AttackDamage;
                                if(!(skill?Skills:Damage).Contains(item.Key)||effect==PowerEffect.Resistance&&item.Key=="Pure"){errors.Add("Invalid target: "+item.Key);continue;}
                                if(effect==PowerEffect.Resistance) {
                                    if(!Enum.TryParse(item.Value.Scalar,out Resistance modifier)||!Enum.IsDefined(typeof(Resistance),modifier)){errors.Add("Invalid resistance: "+item.Value.Scalar);continue;}
                                    if(modifier!=Resistance.Normal)effects.Add(new EffectValue(effect,0,item.Key,modifier));
                                } else {
                                    if(!Number(item.Value.Scalar,out var n)){errors.Add("Invalid number: "+item.Value.Scalar);continue;}
                                    if(n==0)continue;
                                    if(skill) {
                                        if(!skillTargets.TryGetValue(effect,out var used))skillTargets[effect]=used=new HashSet<string>();
                                        if(!used.Contains(item.Key)&&used.Count>=(effect==PowerEffect.SkillLevel?2:1)){errors.Add("Too many skill targets for "+effect);continue;}
                                        used.Add(item.Key);
                                    }
                                    effects.Add(new EffectValue(effect,n,item.Key));
                                }
                            }
                        } else if(effect==PowerEffect.ColdResistance||effect==PowerEffect.SailingPower||effect==PowerEffect.DoubleImpactDamage||effect==PowerEffect.TamingBoost) {
                            if(!bool.TryParse(entry.Value.Scalar,out var flag))errors.Add("Invalid toggle: "+entry.Key);else if(flag)effects.Add(new EffectValue(effect,0,"",Resistance.Normal,true));
                        } else if(!Number(entry.Value.Scalar,out var n))errors.Add("Invalid number: "+entry.Key);else if(n!=0)effects.Add(new EffectValue(effect,n));
                    }
                }
            }
            return new PowerDefinitions(powers);
        }
        static bool Number(string? text,out float number)=>float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&!float.IsNaN(number)&&!float.IsInfinity(number);
    }
}
