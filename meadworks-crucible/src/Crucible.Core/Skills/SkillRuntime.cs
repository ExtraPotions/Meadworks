using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Decides which levels the local player's client writes to its own player ZDO (GrindstoneSkills
    /// <c>CustomSkillLevels</c>): once per interval, only levels that changed, everything again for a new player.
    /// Levels live on the player's own client like the game's skills; peers read them with <see cref="PublishedLevel"/>.
    /// </summary>
    public sealed class SkillLevelPublisher {
        readonly Dictionary<string,float> published=new Dictionary<string,float>(StringComparer.Ordinal);
        readonly float interval;
        float timer;
        long player;
        bool hasPlayer;
        public SkillLevelPublisher(float interval=1f){
            if(float.IsNaN(interval)||interval<=0f)throw new ArgumentOutOfRangeException(nameof(interval));
            this.interval=interval;
        }
        public IReadOnlyList<KeyValuePair<string,float>> Tick(long playerId,float deltaTime,IEnumerable<KeyValuePair<string,float>> levels)=>
            Due(playerId,deltaTime)?Publish(levels):Array.Empty<KeyValuePair<string,float>>();
        /// <summary>
        /// The timing half of <see cref="Tick"/>: advances the interval and returns true when levels should be published
        /// now (then call <see cref="Publish"/>). Lets a caller skip reading levels on the frames in between.
        /// </summary>
        public bool Due(long playerId,float deltaTime){
            if(!hasPlayer||player!=playerId){published.Clear();player=playerId;hasPlayer=true;}
            timer+=float.IsNaN(deltaTime)||deltaTime<0f?0f:deltaTime;
            if(timer<interval)return false;
            timer=0f;
            return true;
        }
        /// <summary>The writing half of <see cref="Tick"/>: the levels that changed since they were last published.</summary>
        public IReadOnlyList<KeyValuePair<string,float>> Publish(IEnumerable<KeyValuePair<string,float>> levels){
            var writes=new List<KeyValuePair<string,float>>();
            foreach(var pair in levels??Enumerable.Empty<KeyValuePair<string,float>>()){
                float level=PublishedLevel.Sanitize(pair.Value);
                if(published.TryGetValue(pair.Key,out float last)&&last==level)continue;
                published[pair.Key]=level;
                writes.Add(new KeyValuePair<string,float>(pair.Key,level));
            }
            return writes;
        }
        public void Reset(){published.Clear();timer=0f;hasPlayer=false;}
    }

    public sealed class SkillEditDecision {
        internal SkillEditDecision(bool allowed,ConfigBlockReason reason,bool noPlayer){Allowed=allowed;Reason=reason;NoPlayer=noPlayer;}
        public bool Allowed { get; }
        public ConfigBlockReason Reason { get; }
        /// <summary>Menu or dedicated server: there is no local character whose skills could change.</summary>
        public bool NoPlayer { get; }
        public string Message=>NoPlayer?"No local character has skills to edit.":Allowed?"":AuthorityGate.Describe(Reason);
        public override string ToString()=>Allowed?"Allowed":NoPlayer?"Blocked:NoPlayer":"Blocked:"+Reason;
    }

    /// <summary>
    /// Skill editing (setting, raising or resetting levels outside play: console raiseskill and resetskill for custom
    /// skills, Saga's editor) is a Cheat scope (roster: Saga "cheat: skill editing"). Single player and the host edit;
    /// on a Crucible server only admins edit; a vanilla server or pending authority refuses. Earning experience by
    /// playing is never gated: levels live on the player's own client, as in the game.
    /// </summary>
    public static class SkillEditPolicy {
        public const ConfigScope Scope=ConfigScope.Cheat;
        public static SkillEditDecision Decide(AuthorityFacts facts){
            if(facts==null)throw new ArgumentNullException(nameof(facts));
            if(facts.Role==SessionRole.Menu||facts.Role==SessionRole.DedicatedServer)return new SkillEditDecision(false,ConfigBlockReason.None,true);
            var decision=AuthorityGate.Decide(Scope,facts);
            return new SkillEditDecision(decision.Allowed,decision.Reason,false);
        }
    }

    /// <summary>Skill names through the C05 catalogs, and the words the game looks skills up by.</summary>
    public static class SkillTexts {
        /// <summary>
        /// The localization word the skills panel and the level-up message use: <c>"skill_" + type.ToString().ToLower()</c>
        /// (<c>Skills.RaiseSkill</c>), which is the number for a custom skill.
        /// </summary>
        public static string GameWord(SkillId id){
            var vanilla=VanillaSkills.NameOf(id);
            return "skill_"+(vanilla!=null?vanilla.ToLowerInvariant():id.ToString());
        }
        public static string Name(SkillDefinition skill,ProductTranslations? translations,string? language)=>Text(skill,skill.NameKey,skill.Name,translations,language);
        public static string Description(SkillDefinition skill,ProductTranslations? translations,string? language)=>Text(skill,skill.DescriptionKey,skill.Description,translations,language);
        static string Text(SkillDefinition skill,string key,string fallback,ProductTranslations? translations,string? language){
            if(skill==null)throw new ArgumentNullException(nameof(skill));
            if(translations==null||!translations.Keys.Contains(key))return fallback;
            return translations.Text(key,language);
        }
    }
}
