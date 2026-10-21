using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// A custom skill a product registers. The <see cref="Identity"/> fixes the skill's number forever (saved levels
    /// are stored under it); choose a product-independent Meadworks identity (<c>meadworks_husbandry</c>) so a skill
    /// can move between products, and list the source mod's identities in <see cref="ImportFrom"/> to carry levels
    /// over once per character. Keeping a source mod's identity instead (<c>grindstone_husbandry</c>) reads its saved
    /// level in place, but then the two mods cannot run side by side.
    /// </summary>
    public sealed class SkillDefinition {
        public SkillDefinition(string owner,string identity,string name,string description,float increaseStep=1f,string? publishKey=null,IEnumerable<string>? importFrom=null,string? textKey=null){
            if(string.IsNullOrWhiteSpace(owner))throw new ArgumentException("A skill needs its product's id.",nameof(owner));
            Identity=SkillIdentities.Require(identity,nameof(identity));
            if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("A skill needs an English name.",nameof(name));
            if(float.IsNaN(increaseStep)||float.IsInfinity(increaseStep)||increaseStep<=0f||increaseStep>100f)throw new ArgumentOutOfRangeException(nameof(increaseStep),"The increase step is above 0 and at most 100.");
            Owner=owner;Name=name.Trim();Description=description??"";IncreaseStep=increaseStep;
            Id=SkillId.FromIdentity(identity);
            CommandName=new string(Name.Where(c=>!char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();
            PublishKey=publishKey??identity+"_level";
            if(!SkillIdentities.IsValid(PublishKey))throw new ArgumentException("Publish keys are 3 to 64 characters of a-z, 0-9, '_' and '.'.",nameof(publishKey));
            NameKey=textKey??identity;
            if(!TranslationJson.IsValidKey(NameKey))throw new ArgumentException(textKey==null?"This identity is not a translation key; pass textKey.":"Invalid text key.",nameof(textKey));
            DescriptionKey=NameKey+"_description";
            if(!TranslationJson.IsValidKey(DescriptionKey))throw new ArgumentException("The text key is too long.",nameof(textKey));
            var sources=new List<string>();
            foreach(var source in importFrom??Array.Empty<string>()){
                SkillIdentities.Require(source,nameof(importFrom));
                if(source==identity)throw new ArgumentException("A skill cannot import from itself.",nameof(importFrom));
                if(!sources.Contains(source))sources.Add(source);
            }
            ImportFrom=sources.AsReadOnly();
        }
        public string Owner { get; }
        public string Identity { get; }
        public SkillId Id { get; }
        /// <summary>English name: the skills panel's fallback and, without spaces and in lower case, the console word.</summary>
        public string Name { get; }
        public string Description { get; }
        /// <summary>The game's <c>SkillDef.m_increseStep</c>: experience per unit of raise factor.</summary>
        public float IncreaseStep { get; }
        public string CommandName { get; }
        /// <summary>Float key on the player's own ZDO where the owning client publishes this level for other peers.</summary>
        public string PublishKey { get; }
        /// <summary>Translation keys in the product's C05 catalog.</summary>
        public string NameKey { get; }
        public string DescriptionKey { get; }
        /// <summary>Identities of other mods' skills whose saved level is imported once per character, first found wins.</summary>
        public IReadOnlyList<string> ImportFrom { get; }
        public override string ToString()=>Identity+" ("+Id+")";
    }

    public sealed class SkillRegistration:IDisposable {
        readonly SkillRegistry registry;
        internal SkillRegistration(SkillRegistry registry,SkillDefinition definition){this.registry=registry;Definition=definition;}
        public SkillDefinition Definition { get; }
        public void Dispose()=>registry.Remove(this);
    }

    /// <summary>Every custom skill products registered. Identities, numbers, console words and publish keys are unique.</summary>
    public sealed class SkillRegistry {
        readonly object gate=new object();
        readonly Dictionary<SkillId,SkillRegistration> skills=new Dictionary<SkillId,SkillRegistration>();
        long revision;
        public long Revision=>Interlocked.Read(ref revision);

        public SkillRegistration Register(SkillDefinition definition){
            if(definition==null)throw new ArgumentNullException(nameof(definition));
            if(definition.CommandName=="all"||VanillaSkills.TryParse(definition.CommandName,out _))throw new InvalidOperationException("The skill name "+definition.Name+" is a game skill's console word.");
            var registration=new SkillRegistration(this,definition);
            lock(gate){
                if(skills.TryGetValue(definition.Id,out var existing))
                    throw new InvalidOperationException(existing.Definition.Identity==definition.Identity?"The skill "+definition.Identity+" is already registered by "+existing.Definition.Owner+".":"The skill "+definition.Identity+" has the same number as "+existing.Definition.Identity+"; choose another identity.");
                foreach(var other in skills.Values.Select(r=>r.Definition)){
                    if(other.CommandName==definition.CommandName)throw new InvalidOperationException("The skill name "+definition.Name+" is already used by "+other.Identity+".");
                    if(other.PublishKey==definition.PublishKey)throw new InvalidOperationException("The publish key "+definition.PublishKey+" is already used by "+other.Identity+".");
                }
                skills.Add(definition.Id,registration);
            }
            Interlocked.Increment(ref revision);
            return registration;
        }

        internal void Remove(SkillRegistration registration){
            bool removed=false;
            lock(gate){
                if(skills.TryGetValue(registration.Definition.Id,out var current)&&ReferenceEquals(current,registration))removed=skills.Remove(registration.Definition.Id);
            }
            if(removed)Interlocked.Increment(ref revision);
        }

        public void Clear(){lock(gate)skills.Clear();Interlocked.Increment(ref revision);}

        public bool TryGet(SkillId id,out SkillDefinition definition){
            lock(gate){
                if(skills.TryGetValue(id,out var registration)){definition=registration.Definition;return true;}
            }
            definition=null!;return false;
        }

        public bool IsRegistered(SkillId id){lock(gate)return skills.ContainsKey(id);}

        /// <summary>By console word (spaces ignored), identity or number, ignoring case; null when none matches.</summary>
        public SkillDefinition? Find(string? name){
            if(string.IsNullOrWhiteSpace(name))return null;
            string word=new string(name!.Where(c=>!char.IsWhiteSpace(c)).ToArray());
            lock(gate){
                foreach(var definition in skills.Values.Select(r=>r.Definition)){
                    if(string.Equals(definition.CommandName,word,StringComparison.OrdinalIgnoreCase)||string.Equals(definition.Identity,word,StringComparison.OrdinalIgnoreCase))return definition;
                }
                if(int.TryParse(word,NumberStyles.None,CultureInfo.InvariantCulture,out int number)&&number>=0&&skills.TryGetValue(SkillId.FromRaw(number),out var byNumber))return byNumber.Definition;
            }
            return null;
        }

        public IReadOnlyList<SkillDefinition> All{get{lock(gate)return skills.Values.Select(r=>r.Definition).OrderBy(d=>d.Identity,StringComparer.Ordinal).ToArray();}}
        public IReadOnlyList<SkillDefinition> Owned(string owner){lock(gate)return skills.Values.Select(r=>r.Definition).Where(d=>d.Owner==owner).OrderBy(d=>d.Identity,StringComparer.Ordinal).ToArray();}
    }
}
