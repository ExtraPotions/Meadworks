using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>One source setting mapped onto one product setting (Wildroot import-mapping rule shape).</summary>
    public sealed class ConversionRule {
        /// <summary>Source id "Mod.Section.Key".</summary>
        public string Source { get; }
        /// <summary>Target id "Section.Key" in the product's config; empty for metadata-only rules.</summary>
        public string Target { get; }
        /// <summary>Boolean, Int32, Single, Color, String, KeyCode, KeyboardShortcut, or an enum name whose values are the constraints.</summary>
        public string Type { get; }
        /// <summary>"From low to high" for numbers, or the comma-separated values of an enum.</summary>
        public string Constraints { get; }
        public bool MetadataOnly { get; }
        public bool ActionBinding { get; }
        public ConversionRule(string source,string target,string type,string constraints="",bool metadataOnly=false,bool actionBinding=false){
            if(string.IsNullOrWhiteSpace(source)||source.Length>256)throw new ArgumentException("Rule needs a source id.",nameof(source));
            if(!metadataOnly&&(string.IsNullOrWhiteSpace(target)||target.Length>256))throw new ArgumentException("Rule needs a target id.",nameof(target));
            if(string.IsNullOrWhiteSpace(type))throw new ArgumentException("Rule needs a type.",nameof(type));
            Source=source;Target=target??"";Type=type;Constraints=constraints??"";MetadataOnly=metadataOnly;ActionBinding=actionBinding;
        }
    }

    /// <summary>
    /// A mapping file: <c>{"schema":1,"sourceMod":"PlantEasily","targetSchema":1,"rules":[...]}</c>. Unknown fields are
    /// ignored so newer tools can add metadata; a newer schema is refused.
    /// </summary>
    public sealed class ConversionMapping {
        public const int SupportedSchema=1;
        public int Schema { get; }
        public string SourceMod { get; }
        /// <summary>The product config schema the targets belong to; 0 when the mapping does not say.</summary>
        public int TargetSchema { get; }
        public IReadOnlyList<ConversionRule> Rules { get; }
        public ConversionMapping(int schema,string sourceMod,int targetSchema,IEnumerable<ConversionRule> rules){
            if(schema<1)throw new ArgumentOutOfRangeException(nameof(schema));
            if(schema>SupportedSchema)throw new NotSupportedException("Mapping schema "+schema.ToString(CultureInfo.InvariantCulture)+" is newer than this Crucible understands; update Crucible.");
            if(targetSchema<0)throw new ArgumentOutOfRangeException(nameof(targetSchema));
            var list=(rules??throw new ArgumentNullException(nameof(rules))).ToList();
            if(list.Any(r=>r==null))throw new ArgumentException("Null rule.",nameof(rules));
            var duplicate=list.GroupBy(r=>r.Source,StringComparer.Ordinal).FirstOrDefault(g=>g.Count()>1);
            if(duplicate!=null)throw new ArgumentException("Duplicate source rule: "+duplicate.Key,nameof(rules));
            Schema=schema;SourceMod=sourceMod??"";TargetSchema=targetSchema;Rules=list.AsReadOnly();
        }
        /// <exception cref="FormatException">Malformed JSON or rules.</exception>
        /// <exception cref="NotSupportedException">A newer mapping schema.</exception>
        public static ConversionMapping Parse(string json){
            if(!(MiniJson.Parse(json) is Dictionary<string,object?> root))throw new FormatException("A mapping file is a JSON object.");
            int schema=Int(root,"schema",-1);if(schema<1)throw new FormatException("Mapping needs a positive schema.");
            if(schema>SupportedSchema)throw new NotSupportedException("Mapping schema "+schema.ToString(CultureInfo.InvariantCulture)+" is newer than this Crucible understands; update Crucible.");
            if(!root.TryGetValue("rules",out var rawRules)||!(rawRules is List<object?> items))throw new FormatException("Mapping needs a rules array.");
            var rules=new List<ConversionRule>();
            foreach(var item in items){
                if(!(item is Dictionary<string,object?> rule))throw new FormatException("Each rule is a JSON object.");
                try{rules.Add(new ConversionRule(Text(rule,"source"),Text(rule,"target"),Text(rule,"type"),Text(rule,"constraints"),Flag(rule,"metadataOnly"),Flag(rule,"actionBinding")));}
                catch(ArgumentException e){throw new FormatException("Invalid rule: "+e.Message,e);}
            }
            try{return new ConversionMapping(schema,root.TryGetValue("sourceMod",out var mod)&&mod is string name?name:"",Int(root,"targetSchema",0),rules);}
            catch(ArgumentException e){throw new FormatException(e.Message,e);}
        }
        static string Text(Dictionary<string,object?> o,string key)=>o.TryGetValue(key,out var v)&&v!=null?v as string??throw new FormatException(key+" must be a string."):"";
        static bool Flag(Dictionary<string,object?> o,string key)=>o.TryGetValue(key,out var v)&&v!=null&&(v is bool b?b:throw new FormatException(key+" must be true or false."));
        static int Int(Dictionary<string,object?> o,string key,int fallback){
            if(!o.TryGetValue(key,out var v)||v==null)return fallback;
            if(!(v is double d)||d!=Math.Floor(d)||d<0||d>int.MaxValue)throw new FormatException(key+" must be a whole number.");
            return (int)d;
        }
    }

    public sealed class ConversionValue {
        public string SourceId { get; }
        public string TargetId { get; }
        public object Value { get; }
        internal ConversionValue(string source,string target,object value){SourceId=source;TargetId=target;Value=value;}
    }

    public sealed class ConversionConflict {
        /// <summary>The target id, or for a binding collision the shared shortcut.</summary>
        public string TargetId { get; }
        public IReadOnlyList<ConversionValue> Choices { get; }
        internal ConversionConflict(string target,IEnumerable<ConversionValue> choices){TargetId=target;Choices=choices.ToList().AsReadOnly();}
    }

    public sealed class ConversionPreview {
        public IReadOnlyList<ConversionValue> Values { get; }
        /// <summary>One target fed different values by several sources; the player picks a source.</summary>
        public IReadOnlyList<ConversionConflict> Conflicts { get; }
        /// <summary>Two actions on one shortcut; the player picks another key.</summary>
        public IReadOnlyList<ConversionConflict> BindingConflicts { get; }
        /// <summary>Values that do not parse or violate constraints; they block Apply.</summary>
        public IReadOnlyList<string> Errors { get; }
        /// <summary>Source settings no rule knows (often a newer source-mod version); skipped, not blocking.</summary>
        public IReadOnlyList<string> Unknown { get; }
        public IReadOnlyList<string> Metadata { get; }
        /// <summary>True when Apply needs no choices or overrides.</summary>
        public bool CanApply=>Errors.Count==0&&Conflicts.Count==0&&BindingConflicts.Count==0;
        internal ConversionPreview(List<ConversionValue> values,List<ConversionConflict> conflicts,List<ConversionConflict> bindings,List<string> errors,List<string> unknown,List<string> metadata){
            Values=values.AsReadOnly();Conflicts=conflicts.AsReadOnly();BindingConflicts=bindings.AsReadOnly();Errors=errors.AsReadOnly();Unknown=unknown.AsReadOnly();Metadata=metadata.AsReadOnly();
        }
    }

    public sealed class ConversionResult {
        public int Written { get; }
        public bool BackedUp { get; }
        internal ConversionResult(int written,bool backedUp){Written=written;BackedUp=backedUp;}
    }

    /// <summary>The product's config as the converter writes it.</summary>
    public interface IConversionTarget {
        /// <summary>The product config's current schema (MeadworksConfig.EnsureSchema).</summary>
        int SchemaVersion { get; }
        /// <summary>Copies the current file aside; called once, before the first write.</summary>
        void Backup();
        /// <summary>Writes every value or none (target id "Section.Key" to typed value).</summary>
        void WriteAtomic(IReadOnlyDictionary<string,object> values);
    }

    /// <summary>Optional target-side validation of the complete batch before any backup or write.</summary>
    public interface IValidatedConversionTarget:IConversionTarget {
        /// <summary>Checks bound target identities and native value types without changing the target.</summary>
        void Validate(IReadOnlyDictionary<string,object> values);
    }

    /// <summary>
    /// Generalised source-mod config importer (from Wildroot ConfigImporter): Preview parses and validates every source
    /// value against its rule, reports conflicts, binding collisions, errors and unknown settings; Apply refuses errors,
    /// unresolved conflicts, collisions and a target schema other than the mapping's, then backs up once and writes
    /// atomically. Nothing is written when nothing is imported.
    /// </summary>
    public sealed class ConfigConverter {
        readonly ConversionMapping mapping;readonly Dictionary<string,ConversionRule> rules;
        public ConversionMapping Mapping=>mapping;
        public ConfigConverter(ConversionMapping mapping){
            this.mapping=mapping??throw new ArgumentNullException(nameof(mapping));
            rules=mapping.Rules.ToDictionary(r=>r.Source,StringComparer.Ordinal);
        }

        public ConversionPreview Preview(IEnumerable<KeyValuePair<string,string>> settings){
            if(settings==null)throw new ArgumentNullException(nameof(settings));
            var values=new List<ConversionValue>();var errors=new List<string>();var unknown=new List<string>();var metadata=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var setting in settings){
                if(setting.Key==null){errors.Add("Setting without an id");continue;}
                if(!seen.Add(setting.Key)){errors.Add(setting.Key+": duplicate source setting");continue;}
                if(!rules.TryGetValue(setting.Key,out var rule)){unknown.Add(setting.Key);continue;}
                if(rule.MetadataOnly){metadata.Add(setting.Key+": reference-only metadata omitted");continue;}
                try{values.Add(new ConversionValue(setting.Key,rule.Target,Parse(rule,setting.Value)));}
                catch(Exception e)when(e is ArgumentException||e is FormatException||e is OverflowException){errors.Add(setting.Key+": "+e.Message);}
            }
            var conflicts=values.GroupBy(v=>v.TargetId,StringComparer.Ordinal).Where(g=>g.Select(v=>v.Value).Distinct().Count()>1).Select(g=>new ConversionConflict(g.Key,g)).ToList();
            return new ConversionPreview(values,conflicts,Collisions(values).ToList(),errors,unknown,metadata);
        }

        IEnumerable<ConversionConflict> Collisions(IEnumerable<ConversionValue> values)=>
            values.Where(v=>rules.TryGetValue(v.SourceId,out var rule)&&rule.ActionBinding&&!string.Equals((string)v.Value,"None",StringComparison.OrdinalIgnoreCase))
                .GroupBy(v=>(string)v.Value,StringComparer.OrdinalIgnoreCase).Where(g=>g.Select(v=>v.TargetId).Distinct(StringComparer.Ordinal).Count()>1)
                .Select(g=>new ConversionConflict(g.Key,g));

        /// <param name="choices">Target id to the chosen source id, for every conflict.</param>
        /// <param name="bindingOverrides">Target id to a replacement shortcut, for action bindings only.</param>
        public ConversionResult Apply(ConversionPreview preview,IConversionTarget target,IReadOnlyDictionary<string,string> choices,IReadOnlyDictionary<string,string>? bindingOverrides=null){
            if(preview==null)throw new ArgumentNullException(nameof(preview));
            if(target==null)throw new ArgumentNullException(nameof(target));
            if(choices==null)throw new ArgumentNullException(nameof(choices));
            if(preview.Errors.Count!=0)throw new ArgumentException("Import contains invalid settings.",nameof(preview));
            if(mapping.TargetSchema>0){
                if(target.SchemaVersion>mapping.TargetSchema)throw new NotSupportedException("The product config is newer than this mapping; update the product.");
                if(target.SchemaVersion<mapping.TargetSchema)throw new InvalidOperationException("Migrate the product config before importing.");
            }
            var selected=new Dictionary<string,object>(StringComparer.Ordinal);
            foreach(var group in preview.Values.GroupBy(v=>v.TargetId,StringComparer.Ordinal)){
                var options=group.ToArray();
                if(options.Select(v=>v.Value).Distinct().Count()==1){selected.Add(group.Key,options[0].Value);continue;}
                if(!choices.TryGetValue(group.Key,out var source))throw new ArgumentException("Unresolved import conflict: "+group.Key,nameof(choices));
                var match=options.SingleOrDefault(v=>v.SourceId==source)??throw new ArgumentException("Invalid conflict selection: "+group.Key,nameof(choices));
                selected.Add(group.Key,match.Value);
            }
            if(bindingOverrides!=null)foreach(var entry in bindingOverrides){
                var candidate=preview.Values.FirstOrDefault(v=>v.TargetId==entry.Key&&rules[v.SourceId].ActionBinding)??throw new ArgumentException("Unknown action binding override: "+entry.Key,nameof(bindingOverrides));
                selected[entry.Key]=Parse(rules[candidate.SourceId],entry.Value);
            }
            var final=preview.Values.Where(v=>selected.ContainsKey(v.TargetId)).Select(v=>new ConversionValue(v.SourceId,v.TargetId,selected[v.TargetId]));
            if(Collisions(final).Any())throw new ArgumentException("Unresolved shortcut collision.",nameof(bindingOverrides));
            if(selected.Count==0)return new ConversionResult(0,false);
            var batch=new ReadOnlyDictionary<string,object>(selected);
            if(target is IValidatedConversionTarget validated)validated.Validate(batch);
            target.Backup();target.WriteAtomic(batch);
            return new ConversionResult(selected.Count,true);
        }

        static object Parse(ConversionRule rule,string? text){
            if(text==null)throw new ArgumentException("Missing value");
            object value;
            switch(rule.Type){
                case "Boolean":value=bool.Parse(text.Trim());break;
                case "Int32":value=int.Parse(text.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture);break;
                case "Single":
                    var number=float.Parse(text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);
                    if(float.IsNaN(number)||float.IsInfinity(number))throw new ArgumentException("Nonfinite value");
                    value=number;break;
                case "Color":if(!Regex.IsMatch(text.Trim(),@"\A[0-9a-fA-F]{8}\z"))throw new ArgumentException("Color requires RGBA hex");value=text.Trim().ToUpperInvariant();break;
                case "String":value=text;break;
                case "KeyCode":case "KeyboardShortcut":
                    if(!HotkeyChord.TryParse(text,out var chord))throw new ArgumentException("Unknown key or shortcut");
                    if(rule.Type=="KeyCode"&&chord.Modifiers.Count>0)throw new ArgumentException("A key code takes one key");
                    value=chord.IsNone?"None":string.Join(" + ",new[]{chord.Main}.Concat(chord.Modifiers.OrderBy(m=>m,StringComparer.Ordinal)));break;
                default:
                    var allowed=rule.Constraints.Split(',').Select(v=>v.Trim()).Where(v=>v.Length>0).ToArray();
                    if(allowed.Length==0||!allowed.Contains(text.Trim(),StringComparer.Ordinal))throw new ArgumentException("Unknown enum value");
                    value=text.Trim();break;
            }
            if(rule.Constraints.StartsWith("From ",StringComparison.Ordinal)){
                var range=Regex.Match(rule.Constraints,@"\AFrom (.+) to (.+)\z");if(!range.Success)throw new ArgumentException("Invalid range constraint");
                double low=double.Parse(range.Groups[1].Value,CultureInfo.InvariantCulture),high=double.Parse(range.Groups[2].Value,CultureInfo.InvariantCulture),actual=Convert.ToDouble(value,CultureInfo.InvariantCulture);
                if(actual<low||actual>high)throw new ArgumentException("Outside accepted range");
            }
            return value;
        }
    }

    /// <summary>Reads a BepInEx .cfg file's values as "Prefix.Section.Key" settings for <see cref="ConfigConverter.Preview"/>.</summary>
    public static class CfgText {
        const int MaxLength=1024*1024;
        public static IReadOnlyList<KeyValuePair<string,string>> Read(string text,string prefix){
            if(text==null||text.Length>MaxLength)throw new ArgumentException("Config text exceeds bounds.",nameof(text));
            if(string.IsNullOrWhiteSpace(prefix))throw new ArgumentException("Expected a source prefix.",nameof(prefix));
            var result=new List<KeyValuePair<string,string>>();string? section=null;
            foreach(var raw in text.Split('\n')){
                var line=raw.Trim();
                if(line.Length==0||line[0]=='#')continue;
                if(line[0]=='['&&line[line.Length-1]==']'){section=line.Substring(1,line.Length-2).Trim();continue;}
                var cut=line.IndexOf('=');if(cut<=0||section==null)continue;
                result.Add(new KeyValuePair<string,string>(prefix+"."+section+"."+line.Substring(0,cut).Trim(),line.Substring(cut+1).Trim()));
            }
            return result.AsReadOnly();
        }
    }

    public sealed class RenamedKey {
        public string Section { get; }
        public string OldKey { get; }
        public string NewKey { get; }
        public RenamedKey(string section,string oldKey,string newKey){
            if(string.IsNullOrWhiteSpace(section)||string.IsNullOrWhiteSpace(oldKey)||string.IsNullOrWhiteSpace(newKey))throw new ArgumentException("Rename needs section, old key and new key.");
            if(string.Equals(oldKey,newKey,StringComparison.Ordinal))throw new ArgumentException("Rename must change the key.");
            Section=section;OldKey=oldKey;NewKey=newKey;
        }
        public string OldId=>Section+"."+OldKey;
        public string NewId=>Section+"."+NewKey;
    }

    public sealed class RenameCarry {
        public RenamedKey Rename { get; }
        public string Value { get; }
        internal RenameCarry(RenamedKey rename,string value){Rename=rename;Value=value;}
    }

    public sealed class RenamePlan {
        public IReadOnlyList<RenameCarry> Carry { get; }
        /// <summary>Old "Section.Key" lines to drop from the file's orphaned entries after carrying.</summary>
        public IReadOnlyList<string> Consume { get; }
        public IReadOnlyList<string> Skipped { get; }
        internal RenamePlan(List<RenameCarry> carry,List<string> skipped){Carry=carry.AsReadOnly();Consume=carry.Select(c=>c.Rename.OldId).ToList().AsReadOnly();Skipped=skipped.AsReadOnly();}
    }

    /// <summary>
    /// Pure form of Burrow's RenamedKeys: an orphaned old line carries its value to the new key unless the file already
    /// has the new key; the first rename to a new key wins. The plugin adapter applies the plan to BepInEx's orphaned entries.
    /// </summary>
    public static class RenamedKeys {
        public static RenamePlan Plan(IReadOnlyDictionary<string,string> orphans,IEnumerable<string> presentKeys,IEnumerable<RenamedKey> renames){
            if(orphans==null)throw new ArgumentNullException(nameof(orphans));
            var present=new HashSet<string>(presentKeys??Array.Empty<string>(),StringComparer.Ordinal);
            var carry=new List<RenameCarry>();var skipped=new List<string>();
            foreach(var rename in renames??throw new ArgumentNullException(nameof(renames))){
                if(!orphans.TryGetValue(rename.OldId,out var value))continue;
                if(present.Contains(rename.NewId)){skipped.Add(rename.OldId+": "+rename.NewId+" already set; old line kept");continue;}
                if(carry.Any(c=>c.Rename.NewId==rename.NewId)){skipped.Add(rename.OldId+": "+rename.NewId+" already carried from another rename");continue;}
                carry.Add(new RenameCarry(rename,value));
            }
            return new RenamePlan(carry,skipped);
        }
    }
}
