using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Wildroot {
 /// <summary>A reference mod Wildroot replaces: its plugin GUID (for step-aside), config file and import prefix.</summary>
 public sealed class ReferenceMod {
  public SourceMod Mod{get;}public string ConfigFile{get;}public string Prefix{get;}
  public ReferenceMod(string guid,string name,string configFile,string prefix){Mod=new SourceMod(guid,name);ConfigFile=configFile;Prefix=prefix;}
 }
 public enum ImportStatus {Refused,NothingFound,Blocked,Applied}
 public sealed class ImportOutcome {
  public ImportStatus Status{get;}public int Written{get;}public IReadOnlyList<string> Report{get;}
  internal ImportOutcome(ImportStatus status,int written,List<string> report){Status=status;Written=written;Report=report.AsReadOnly();}
 }
 /// <summary>
 /// Wildroot's reference import over the Crucible C09 converter: reads PlantEasily and PlantEverything settings without
 /// changing them, reports every disposition, refuses to write while conflicts, collisions or errors remain, and backs
 /// up the Wildroot config once before one atomic write. Only the machine that owns its config imports: a remote
 /// client's server-owned values belong to the server.
 /// </summary>
 public static class ReferenceImport {
  public static IReadOnlyList<ReferenceMod> Sources{get;}=new[]{
   new ReferenceMod("advize.PlantEasily","PlantEasily","advize.PlantEasily.cfg","PlantEasily"),
   new ReferenceMod("advize.PlantEverything","PlantEverything","advize.PlantEverything.cfg","PlantEverything"),
  };
  /// <summary>Parses the mapping file and binds it to the Wildroot config schema, so Apply refuses older or newer configs.</summary>
  public static ConfigConverter Converter(string mappingJson){
   var parsed=ConversionMapping.Parse(mappingJson);
   return new ConfigConverter(new ConversionMapping(parsed.Schema,string.IsNullOrEmpty(parsed.SourceMod)?"PlantEasily+PlantEverything":parsed.SourceMod,WildrootSettings.SchemaVersion,parsed.Rules));
  }
  public static bool Allowed(SessionRole role)=>role!=SessionRole.RemoteClient;
  /// <summary>"Target=Value; Target=Value" pairs; null when the text is malformed or names a target twice.</summary>
  public static IReadOnlyDictionary<string,string>? ParsePairs(string? text){
   var result=new Dictionary<string,string>(StringComparer.Ordinal);
   foreach(var raw in (text??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries)){
    var part=raw.Trim();if(part.Length==0)continue;var cut=part.IndexOf('=');
    if(cut<=0||cut==part.Length-1)return null;var key=part.Substring(0,cut).Trim();var value=part.Substring(cut+1).Trim();
    if(key.Length==0||value.Length==0||result.ContainsKey(key))return null;result.Add(key,value);
   }
   return result;
  }
  public static ImportOutcome Run(SessionRole role,ConfigConverter converter,IEnumerable<KeyValuePair<string,string>> settings,IConversionTarget target,string? choices,string? bindingOverrides){
   if(converter==null)throw new ArgumentNullException(nameof(converter));if(settings==null)throw new ArgumentNullException(nameof(settings));if(target==null)throw new ArgumentNullException(nameof(target));
   var report=new List<string>();
   if(!Allowed(role)){report.Add("refused: connected to a server; the server owns Wildroot's world settings. Import in single player or on the server.");return new ImportOutcome(ImportStatus.Refused,0,report);}
   var all=settings.ToList();
   var rules=converter.Mapping.Rules.ToDictionary(r=>r.Source,StringComparer.Ordinal);
   var kept=new List<KeyValuePair<string,string>>();var superseded=new List<string>();var unbound=new List<string>();
   foreach(var setting in all){
    if(setting.Key!=null&&rules.TryGetValue(setting.Key,out var rule)&&!rule.MetadataOnly&&WildrootSettings.Superseded.TryGetValue(rule.Target,out var reason))superseded.Add("superseded "+setting.Key+" -> "+rule.Target+": "+reason);
    // A window or panel key opens from the MeadWorks menu and stays unbound: the reference's old key is never written back.
    else if(setting.Key!=null&&rules.TryGetValue(setting.Key,out var menuRule)&&!menuRule.MetadataOnly&&WildrootSettings.UnboundByDefault.Contains(menuRule.Target))unbound.Add("kept unbound "+setting.Key+" -> "+menuRule.Target+": opens from the MeadWorks menu; bind it in Wildroot's settings if wanted.");
    else kept.Add(setting);
   }
   if(all.Count==0){report.Add("nothing found: no PlantEasily or PlantEverything settings to import.");return new ImportOutcome(ImportStatus.NothingFound,0,report);}
   var preview=converter.Preview(kept);
   report.Add("Wildroot import: "+preview.Values.Count+" values, "+preview.Conflicts.Count+" conflicts, "+preview.BindingConflicts.Count+" shortcut collisions, "+preview.Errors.Count+" errors, "+preview.Unknown.Count+" unknown, "+preview.Metadata.Count+" metadata, "+superseded.Count+" superseded, "+unbound.Count+" kept unbound.");
   foreach(var value in preview.Values)report.Add("value "+value.SourceId+" -> "+value.TargetId+" = "+Convert.ToString(value.Value,CultureInfo.InvariantCulture));
   foreach(var conflict in preview.Conflicts)report.Add("conflict "+conflict.TargetId+": "+string.Join(" | ",conflict.Choices.Select(c=>c.SourceId+"="+Convert.ToString(c.Value,CultureInfo.InvariantCulture)))+" (choose with Import.ConflictChoices: "+conflict.TargetId+"=<source>)");
   foreach(var collision in preview.BindingConflicts)report.Add("collision "+collision.TargetId+": "+string.Join(", ",collision.Choices.Select(c=>c.TargetId))+" (set another key with Import.BindingOverrides: <target>=<key>)");
   foreach(var error in preview.Errors)report.Add("error "+error);
   foreach(var unknown in preview.Unknown)report.Add("unknown "+unknown+": skipped (newer reference version?)");
   foreach(var metadata in preview.Metadata)report.Add("metadata "+metadata);
   report.AddRange(superseded);report.AddRange(unbound);
   var choiceMap=ParsePairs(choices);var overrideMap=ParsePairs(bindingOverrides);
   if(choiceMap==null||overrideMap==null){report.Add("blocked: Import.ConflictChoices and Import.BindingOverrides take Target=Value pairs separated by semicolons.");return new ImportOutcome(ImportStatus.Blocked,0,report);}
   if(preview.Values.Count==0&&preview.Errors.Count==0){report.Add("nothing to import: no reference value maps to a Wildroot setting.");return new ImportOutcome(ImportStatus.NothingFound,0,report);}
   try{
    var result=converter.Apply(preview,target,choiceMap,overrideMap.Count==0?null:overrideMap);
    report.Add("applied "+result.Written+" values; the previous Wildroot config was backed up first.");
    return new ImportOutcome(result.Written==0?ImportStatus.NothingFound:ImportStatus.Applied,result.Written,report);
   }catch(Exception error)when(error is ArgumentException||error is NotSupportedException||error is InvalidOperationException||error is FormatException){
    report.Add("blocked: "+error.Message+" Nothing was written.");return new ImportOutcome(ImportStatus.Blocked,0,report);
   }
  }
 }
}
