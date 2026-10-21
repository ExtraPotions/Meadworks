using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace ExtraPotions.Wildroot {
 public sealed class ExtraResourceDefinition {public string Prefab{get;}public string Material{get;}public int Cost{get;}public bool GroundOnly{get;}public string Name{get;}public string Description{get;}public ExtraResourceDefinition(string prefab,string material,int cost,bool groundOnly,string name,string description){Prefab=prefab;Material=material;Cost=cost;GroundOnly=groundOnly;Name=name;Description=description;}}
 public sealed class ExtraResourceImportResult {public bool Valid{get;}public IReadOnlyList<ExtraResourceDefinition> Entries{get;}public IReadOnlyList<string> Warnings{get;}public ExtraResourceImportResult(bool valid,IReadOnlyList<ExtraResourceDefinition> entries,IReadOnlyList<string> warnings){Valid=valid;Entries=new List<ExtraResourceDefinition>(entries).AsReadOnly();Warnings=new List<string>(warnings).AsReadOnly();}}
 public static class ExtraResourceImporter {
  public static ExtraResourceImportResult Parse(string text,IPrefabCatalog prefabs){
   if(prefabs==null)throw new ArgumentNullException(nameof(prefabs));var entries=new List<ExtraResourceDefinition>();var warnings=new List<string>();
   try{
    if(text==null||text.Length>1048576)throw new ArgumentException("Extra resource size exceeded");
    foreach(var block in Blocks(text)){
     JObject obj;using(var reader=new JsonTextReader(new StringReader(block)){MaxDepth=32})obj=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
     try{
      string prefab=ReadText(obj,"prefabName");if(prefab.StartsWith("PE_Fake",StringComparison.Ordinal))continue;
      string material=ReadText(obj,"resourceName");if(obj["resourceCost"]?.Type!=JTokenType.Integer)throw new ArgumentException("Cost must be an integer");int cost=obj["resourceCost"]!.Value<int>();
      if(cost<=0||!prefabs.HasPrefab(prefab)||!prefabs.HasItem(material))throw new ArgumentException("Unresolved prefab/material or nonpositive cost");
      if(obj["groundOnly"]?.Type!=JTokenType.Boolean)throw new ArgumentException("groundOnly must be boolean");
      entries.Add(new ExtraResourceDefinition(prefab,material,cost,obj["groundOnly"]!.Value<bool>(),ReadText(obj,"pieceName"),ReadText(obj,"pieceDescription")));
     }catch(Exception error)when(error is ArgumentException||error is OverflowException||error is FormatException){warnings.Add("Skipped extra resource: "+error.Message);}
    }
    return new ExtraResourceImportResult(true,entries,warnings);
   }catch(Exception error)when(error is JsonException||error is ArgumentException){warnings.Add(error.Message);return new ExtraResourceImportResult(false,Array.Empty<ExtraResourceDefinition>(),warnings);}
  }
  static string ReadText(JObject obj,string name){if(obj[name]?.Type!=JTokenType.String)throw new ArgumentException(name+" must be text");return obj[name]!.Value<string>()??"";}
  static IEnumerable<string> Blocks(string text){
   int index=0,count=0;
   while(index<text.Length){
    while(index<text.Length&&(char.IsWhiteSpace(text[index])||text[index]==';'))index++;
    if(index==text.Length)yield break;if(text[index]!='{'||++count>4096)throw new ArgumentException("Invalid extra-resource object boundary");
    int start=index,depth=0;bool quoted=false,escaped=false;
    for(;index<text.Length;index++){
     char c=text[index];
     if(quoted){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c=='"')quoted=false;continue;}
     if(c=='"'){quoted=true;continue;}if(c=='{')depth++;else if(c=='}'&&--depth==0){index++;break;}
    }
    if(depth!=0||quoted)throw new ArgumentException("Truncated extra-resource object");
    yield return text.Substring(start,index-start);
   }
  }
 }
}
