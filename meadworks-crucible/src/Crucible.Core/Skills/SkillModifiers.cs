using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ExtraPotions.Meadworks.Crucible {
    public enum SkillModifierKind {
        /// <summary>Multiplies experience gained (multipliers from every product multiply).</summary>
        Experience,
        /// <summary>Adds to the effective level products read (bonuses add); the saved level never changes.</summary>
        Level,
        /// <summary>Multiplies the share a death takes from this skill (0 protects it fully).</summary>
        DeathLoss
    }

    /// <summary>One product's contribution to one skill. Change <see cref="Value"/> in place; dispose to remove it.</summary>
    public sealed class SkillModifier:IDisposable {
        readonly SkillModifiers owner;
        float value;
        internal SkillModifier(SkillModifiers owner,string product,SkillId skill,SkillModifierKind kind,float value){this.owner=owner;Owner=product;Skill=skill;Kind=kind;this.value=value;Active=true;}
        public string Owner { get; }
        public SkillId Skill { get; }
        public SkillModifierKind Kind { get; }
        public bool Active { get; internal set; }
        public float Value {
            get=>Volatile.Read(ref value);
            set{
                if(!Active)throw new ObjectDisposedException(nameof(SkillModifier));
                SkillModifiers.Validate(Kind,value,nameof(value));
                Volatile.Write(ref this.value,value);owner.Touch();
            }
        }
        public void Dispose()=>owner.Remove(this);
    }

    /// <summary>
    /// Per-skill modifiers products contribute, for custom and vanilla skills alike. Experience multipliers multiply
    /// (capped at <see cref="MaximumExperienceMultiplier"/>), level bonuses add, death-loss multipliers multiply
    /// (capped at <see cref="MaximumDeathLossMultiplier"/>). Thread-safe; the revision changes on every change.
    /// </summary>
    public sealed class SkillModifiers {
        public const float MaximumExperienceMultiplier=100f;
        public const float MaximumDeathLossMultiplier=10f;
        readonly object gate=new object();
        readonly List<SkillModifier> modifiers=new List<SkillModifier>();
        long revision;
        public long Revision=>Interlocked.Read(ref revision);
        internal void Touch()=>Interlocked.Increment(ref revision);

        public SkillModifier Add(string owner,SkillId skill,SkillModifierKind kind,float value){
            if(string.IsNullOrWhiteSpace(owner))throw new ArgumentException("A modifier needs its product's id.",nameof(owner));
            if(!Enum.IsDefined(typeof(SkillModifierKind),kind))throw new ArgumentException("Unknown modifier kind.",nameof(kind));
            Validate(kind,value,nameof(value));
            var modifier=new SkillModifier(this,owner,skill,kind,value);
            lock(gate)modifiers.Add(modifier);
            Touch();return modifier;
        }

        internal static void Validate(SkillModifierKind kind,float value,string parameter){
            if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Modifier values must be finite.",parameter);
            if(kind!=SkillModifierKind.Level&&value<0f)throw new ArgumentOutOfRangeException(parameter,"Multipliers cannot be negative.");
        }

        internal void Remove(SkillModifier modifier){
            bool removed;
            lock(gate){removed=modifier.Active&&modifiers.Remove(modifier);modifier.Active=false;}
            if(removed)Touch();
        }

        public int RemoveOwner(string owner){
            int count;
            lock(gate){
                var gone=modifiers.Where(m=>m.Owner==owner).ToArray();
                foreach(var modifier in gone){modifier.Active=false;modifiers.Remove(modifier);}
                count=gone.Length;
            }
            if(count>0)Touch();
            return count;
        }

        public IReadOnlyList<SkillModifier> For(SkillId skill){lock(gate)return modifiers.Where(m=>m.Skill==skill).ToArray();}

        float[] Values(SkillId skill,SkillModifierKind kind){lock(gate)return modifiers.Where(m=>m.Skill==skill&&m.Kind==kind).Select(m=>m.Value).ToArray();}

        public float ExperienceMultiplier(SkillId skill){
            float result=1f;
            foreach(var value in Values(skill,SkillModifierKind.Experience))result*=value;
            return SkillMath.Clamp(result,0f,MaximumExperienceMultiplier);
        }

        public float LevelBonus(SkillId skill){
            float result=0f;
            foreach(var value in Values(skill,SkillModifierKind.Level))result+=value;
            return SkillMath.Clamp(result,-SkillMath.MaxLevel,SkillMath.MaxLevel);
        }

        public float DeathLossMultiplier(SkillId skill){
            float result=1f;
            foreach(var value in Values(skill,SkillModifierKind.DeathLoss))result*=value;
            return SkillMath.Clamp(result,0f,MaximumDeathLossMultiplier);
        }

        /// <summary>The level products should read: the saved level plus every bonus, never below 0 (bonuses may lift it past 100, like the game's status effects).</summary>
        public float EffectiveLevel(SkillId skill,float level)=>MathF.Max(0f,SkillMath.Finite(level)+LevelBonus(skill));
    }
}
