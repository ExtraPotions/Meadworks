using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>One saved skill entry: the raw <c>Skills.SkillType</c> number, level and accumulator.</summary>
    public readonly struct SkillRecord {
        public int Type { get; }
        public float Level { get; }
        public float Accumulator { get; }
        public SkillRecord(int type,float level,float accumulator){Type=type;Level=level;Accumulator=accumulator;}
    }

    /// <summary>
    /// The skills block of a character save, as <c>Skills.Save</c> writes it into the player's ZPackage (read from
    /// assembly_valheim 2026-10-06): int version (2), int count, then per skill int type, float level and, from
    /// version 2, float accumulator; little-endian like BinaryWriter. <c>Skills.Load</c> drops every type the game's
    /// <c>IsSkillValid</c> rejects, so a mod's skill survives a load only while that mod is installed.
    /// </summary>
    public static class SkillSaveData {
        public const int Version=2;
        /// <summary>More entries than any real character has; a larger count is a corrupt block.</summary>
        public const int MaximumCount=4096;

        public static IReadOnlyList<SkillRecord> Read(byte[] data,int offset=0){
            if(data==null)throw new ArgumentNullException(nameof(data));
            if(offset<0||offset>data.Length)throw new ArgumentOutOfRangeException(nameof(offset));
            try{
                using var reader=new BinaryReader(new MemoryStream(data,offset,data.Length-offset,false));
                return Read(reader);
            }catch(EndOfStreamException){throw new FormatException("The skills block is truncated.");}
        }

        /// <summary>Reads from the reader's position; the caller restores the position when it only peeks.</summary>
        public static IReadOnlyList<SkillRecord> Read(BinaryReader reader){
            if(reader==null)throw new ArgumentNullException(nameof(reader));
            try{
                int version=reader.ReadInt32();
                int count=reader.ReadInt32();
                if(count<0||count>MaximumCount)throw new FormatException("The skills block has an invalid count.");
                var records=new List<SkillRecord>(count);
                for(int i=0;i<count;i++){
                    int type=reader.ReadInt32();
                    float level=reader.ReadSingle();
                    float accumulator=version>=2?reader.ReadSingle():0f;
                    records.Add(new SkillRecord(type,level,accumulator));
                }
                return records;
            }catch(EndOfStreamException){throw new FormatException("The skills block is truncated.");}
        }

        public static byte[] Write(IEnumerable<SkillRecord> records){
            var list=(records??throw new ArgumentNullException(nameof(records))).ToList();
            using var stream=new MemoryStream();
            using(var writer=new BinaryWriter(stream)){
                writer.Write(Version);writer.Write(list.Count);
                foreach(var record in list){writer.Write(record.Type);writer.Write(record.Level);writer.Write(record.Accumulator);}
            }
            return stream.ToArray();
        }
    }

    public sealed class SkillImportAction {
        internal SkillImportAction(SkillDefinition target,string sourceIdentity,SkillProgress imported,SkillProgress? previous,bool applied){
            Target=target;SourceIdentity=sourceIdentity;Source=SkillId.FromIdentity(sourceIdentity);Imported=imported;Previous=previous;Applied=applied;Marker=SkillImport.MarkerFor(target.Identity);
        }
        public SkillDefinition Target { get; }
        public string SourceIdentity { get; }
        public SkillId Source { get; }
        public SkillProgress Imported { get; }
        /// <summary>The target's saved progress before the import, when the character had one.</summary>
        public SkillProgress? Previous { get; }
        /// <summary>False when the character already had a level at least as high; the marker is still written.</summary>
        public bool Applied { get; }
        /// <summary>Key in the character's custom data that records the import, so it never runs twice.</summary>
        public string Marker { get; }
        public override string ToString()=>(Applied?"import ":"keep ")+SourceIdentity+" -> "+Target.Identity+" "+Imported;
    }

    /// <summary>
    /// The converter hook for skill levels (C09 counterpart for character data). Runs on the saved records captured
    /// before the game drops unknown types, once per character and target skill: the first listed source found is
    /// copied into the target unless the target already has a level at least as high. The source record is never
    /// removed, so the source mod keeps its own level while it is installed (step-aside).
    /// </summary>
    public static class SkillImport {
        public const string MarkerPrefix="meadworks.skills.imported.";
        public static string MarkerFor(string identity)=>MarkerPrefix+SkillIdentities.Require(identity,nameof(identity));

        public static IReadOnlyList<SkillImportAction> Plan(IEnumerable<SkillRecord> saved,IEnumerable<SkillDefinition> skills,Func<string,bool> hasMarker){
            if(saved==null)throw new ArgumentNullException(nameof(saved));
            if(skills==null)throw new ArgumentNullException(nameof(skills));
            if(hasMarker==null)throw new ArgumentNullException(nameof(hasMarker));
            var byType=new Dictionary<int,SkillRecord>();
            foreach(var record in saved)byType[record.Type]=record;
            var actions=new List<SkillImportAction>();
            foreach(var skill in skills){
                if(skill.ImportFrom.Count==0||hasMarker(SkillImport.MarkerFor(skill.Identity)))continue;
                foreach(var source in skill.ImportFrom){
                    if(!byType.TryGetValue(SkillId.FromIdentity(source).Value,out var record)||float.IsNaN(record.Level)||float.IsInfinity(record.Level))continue;
                    var imported=new SkillProgress(SkillMath.Clamp(record.Level,0f,SkillMath.MaxLevel),record.Accumulator>0f&&!float.IsInfinity(record.Accumulator)?record.Accumulator:0f);
                    SkillProgress? previous=byType.TryGetValue(skill.Id.Value,out var current)?new SkillProgress(current.Level,current.Accumulator):(SkillProgress?)null;
                    bool applied=previous==null||!(previous.Value.Level>=imported.Level);
                    actions.Add(new SkillImportAction(skill,source,imported,previous,applied));
                    break;
                }
            }
            return actions;
        }

        /// <summary>The saved records after a plan: applied targets added or replaced, everything else unchanged.</summary>
        public static IReadOnlyList<SkillRecord> Apply(IEnumerable<SkillRecord> saved,IEnumerable<SkillImportAction> plan){
            var records=(saved??throw new ArgumentNullException(nameof(saved))).ToList();
            foreach(var action in plan??throw new ArgumentNullException(nameof(plan))){
                if(!action.Applied)continue;
                records.RemoveAll(r=>r.Type==action.Target.Id.Value);
                records.Add(new SkillRecord(action.Target.Id.Value,action.Imported.Level,action.Imported.Accumulator));
            }
            return records;
        }
    }

    public sealed class GrindstoneSkillMapping {
        internal GrindstoneSkillMapping(string source,string target,string product,string legacyPublishKey){Source=source;SourceId=SkillId.FromIdentity(source);Target=target;Product=product;LegacyPublishKey=legacyPublishKey;}
        /// <summary>GrindstoneSkills identity (<c>Keys.*SkillName</c>).</summary>
        public string Source { get; }
        public SkillId SourceId { get; }
        /// <summary>The Meadworks identity that imports it.</summary>
        public string Target { get; }
        /// <summary>The Meadworks product that registers the target.</summary>
        public string Product { get; }
        /// <summary>The player ZDO float GrindstoneSkills publishes the level under (<c>Keys.*Level</c>).</summary>
        public string LegacyPublishKey { get; }
    }

    /// <summary>
    /// GrindstoneSkills 0.14.1 (OpenKeep 3.0.0 monorepo) split across Meadworks. It defines four skills of its own
    /// (<c>CustomSkills.Initialize(Sailing, Foraging, Husbandry, Defense)</c>); its Farming, Cooking, Woodcutting,
    /// Pickaxes and Fishing perks extend the game's skills, whose numbers never change, so those levels need no import.
    /// </summary>
    public static class GrindstoneSkillsMap {
        public const string SourceMod="com.GrindstoneSkills";
        static readonly GrindstoneSkillMapping[] Mappings={
            new GrindstoneSkillMapping("grindstone_husbandry","meadworks_husbandry","Shepherd","grindstone_husbandry_level"),
            new GrindstoneSkillMapping("grindstone_sailing","meadworks_sailing","Keel","grindstone_sailing_level"),
            new GrindstoneSkillMapping("grindstone_foraging","meadworks_foraging","Saga","grindstone_foraging_level"),
            new GrindstoneSkillMapping("grindstone_defense","meadworks_defense","Saga","grindstone_defense_level")};
        public static IReadOnlyList<GrindstoneSkillMapping> Skills=>Mappings;

        /// <summary>Game skills GrindstoneSkills extends and the product that owns those perks (roster 2026-10-06).</summary>
        public static IReadOnlyDictionary<SkillId,string> VanillaDomains { get; }=new Dictionary<SkillId,string>{
            [VanillaSkills.Farming]="Wildroot",[VanillaSkills.Cooking]="Larder",[VanillaSkills.WoodCutting]="Saga",[VanillaSkills.Pickaxes]="Saga"};

        public static bool TryMap(string? source,out GrindstoneSkillMapping mapping){
            mapping=Mappings.FirstOrDefault(m=>m.Source==source)!;
            return mapping!=null;
        }
        public static GrindstoneSkillMapping? ForTarget(string? target)=>Mappings.FirstOrDefault(m=>m.Target==target);

        /// <summary>The definition a product registers for a mapped skill: the Meadworks identity, importing from GrindstoneSkills.</summary>
        public static SkillDefinition Definition(string source,string owner,string name,string description,float increaseStep=1f,string? publishKey=null,string? textKey=null){
            if(!TryMap(source,out var mapping))throw new ArgumentException("Not a GrindstoneSkills custom skill: "+source,nameof(source));
            return new SkillDefinition(owner,mapping.Target,name,description,increaseStep,publishKey,new[]{mapping.Source},textKey);
        }
    }
}
