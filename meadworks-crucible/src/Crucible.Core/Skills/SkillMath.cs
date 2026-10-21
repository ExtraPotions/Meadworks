using System;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>A skill's saved state, the game's <c>Skills.Skill</c> m_level and m_accumulator.</summary>
    public readonly struct SkillProgress {
        public float Level { get; }
        /// <summary>Experience gathered toward the next level.</summary>
        public float Accumulator { get; }
        public SkillProgress(float level,float accumulator){Level=level;Accumulator=accumulator;}
        public static SkillProgress Zero=>new SkillProgress(0f,0f);
        public override string ToString()=>Level.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" (+"+Accumulator.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+")";
    }

    public readonly struct SkillRaise {
        public SkillProgress Progress { get; }
        public bool LeveledUp { get; }
        /// <summary>Experience added by this raise (0 when the skill is at 100 or the factor was not a number).</summary>
        public float Gained { get; }
        public SkillRaise(SkillProgress progress,bool leveledUp,float gained){Progress=progress;LeveledUp=leveledUp;Gained=gained;}
    }

    /// <summary>
    /// The game's skill math, read from assembly_valheim (2026-10-06), in float like the game:
    /// <list type="bullet">
    /// <item><c>Skill.Raise(factor)</c>: nothing at level 100; otherwise the accumulator gains
    /// <c>m_increseStep * factor * Game.m_skillGainRate</c>; reaching the requirement adds exactly one level (clamped to
    /// 0..100) and clears the accumulator, however much experience was gained.</item>
    /// <item><c>Skill.GetNextLevelRequirement()</c>: <c>floor(level + 1)^1.5 * 0.5 + 0.5</c>.</item>
    /// <item><c>Skills.LowerAllSkills(factor)</c>: every level loses <c>level * factor</c> and its accumulator;
    /// <c>Skills.OnDeath</c> passes <c>m_DeathLowerFactor * Game.m_skillReductionRate</c>.</item>
    /// <item><c>Player.RaiseSkill</c> multiplies the factor by the status effects' <c>ModifyRaiseSkill</c> first.</item>
    /// </list>
    /// </summary>
    public static class SkillMath {
        public const float MaxLevel=100f;

        public static float NextLevelRequirement(float level)=>MathF.Pow(MathF.Floor(level+1f),1.5f)*0.5f+0.5f;

        public static SkillRaise Raise(SkillProgress progress,float increaseStep,float factor,float gainRate=1f){
            if(progress.Level>=MaxLevel)return new SkillRaise(progress,false,0f);
            float gained=increaseStep*factor*gainRate;
            if(float.IsNaN(gained)||float.IsInfinity(gained))return new SkillRaise(progress,false,0f);
            float accumulator=progress.Accumulator+gained;
            if(accumulator>=NextLevelRequirement(progress.Level))
                return new SkillRaise(new SkillProgress(Clamp(progress.Level+1f,0f,MaxLevel),0f),true,gained);
            return new SkillRaise(new SkillProgress(progress.Level,accumulator),false,gained);
        }

        /// <summary><c>Skill.GetLevelPercentage</c>: share of the way to the next level, 0 at level 100.</summary>
        public static float LevelPercentage(SkillProgress progress)=>
            progress.Level>=MaxLevel?0f:Clamp(progress.Accumulator/NextLevelRequirement(progress.Level),0f,1f);

        public static SkillProgress Lower(SkillProgress progress,float factor)=>new SkillProgress(progress.Level-progress.Level*factor,0f);

        /// <summary>The share a death takes: the game factor times the world's reduction rate times a skill's loss multiplier, within 0..1.</summary>
        public static float DeathLossFactor(float deathLowerFactor,float worldReductionRate,float lossMultiplier){
            float factor=deathLowerFactor*worldReductionRate*lossMultiplier;
            return float.IsNaN(factor)?0f:Clamp(factor,0f,1f);
        }

        /// <summary>The console's raiseskill: add to the level within 0..100, progress kept (<c>Skills.CheatRaiseSkill</c>).</summary>
        public static SkillProgress CheatRaise(SkillProgress progress,float delta)=>
            new SkillProgress(Clamp(Finite(progress.Level)+Finite(delta),0f,MaxLevel),progress.Accumulator);

        public static SkillProgress SetLevel(float level)=>new SkillProgress(Clamp(Finite(level),0f,MaxLevel),0f);

        /// <summary>Experience (before step, factor and rates) from the start of <paramref name="fromLevel"/> to <paramref name="toLevel"/>.</summary>
        public static float ExperienceToReach(float fromLevel,float toLevel){
            float total=0f;
            for(float level=MathF.Floor(Clamp(Finite(fromLevel),0f,MaxLevel));level<Clamp(Finite(toLevel),0f,MaxLevel);level+=1f)total+=NextLevelRequirement(level);
            return total;
        }

        /// <summary>A perk's share at a level: <paramref name="percentAt100"/> percent at level 100, linear from 0 (GrindstoneSkills <c>Share</c>).</summary>
        public static float Share(float percentAt100,float level)=>MathF.Max(0f,Finite(percentAt100))/100f*Clamp(Finite(level)/MaxLevel,0f,1f);

        /// <summary>True when <paramref name="level"/> reaches a milestone; a milestone above 100 is never reached.</summary>
        public static bool Reaches(float level,float milestone)=>milestone<=MaxLevel&&level>=milestone;

        /// <summary>The factor handed to the game's raise: the product's factor times the modifiers' and the setting's multipliers, never negative.</summary>
        public static float ExperienceFactor(float factor,float modifierMultiplier,float settingMultiplier){
            float result=factor*modifierMultiplier*settingMultiplier;
            return float.IsNaN(result)||float.IsInfinity(result)||result<0f?0f:result;
        }

        internal static float Clamp(float value,float min,float max)=>value<min?min:value>max?max:value;
        internal static float Finite(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0f:value;
    }

    /// <summary>A level another peer published on its player ZDO, read defensively.</summary>
    public static class PublishedLevel {
        public static float Sanitize(float level)=>SkillMath.Clamp(SkillMath.Finite(level),0f,SkillMath.MaxLevel);
    }
}
