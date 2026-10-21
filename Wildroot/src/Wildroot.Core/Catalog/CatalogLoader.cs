using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ExtraPotions.Meadworks.Contracts;
namespace ExtraPotions.Wildroot {
 public static class CatalogLoader {
  static string Text(JObject value,string key,string fallback=""){var token=value[key];if(token==null)return fallback;if(token.Type!=JTokenType.String)throw new ArgumentException("Expected text for "+key);return token.Value<string>()??fallback;}
  static double Number(JObject value,string key,double fallback){var token=value[key];if(token==null)return fallback;if(token.Type!=JTokenType.Float&&token.Type!=JTokenType.Integer)throw new ArgumentException("Expected number for "+key);double result=token.Value<double>();if(double.IsNaN(result)||double.IsInfinity(result))throw new ArgumentException("Nonfinite "+key);return result;}
  static int Integer(JObject value,string key,int fallback){var token=value[key];if(token==null)return fallback;if(token.Type!=JTokenType.Integer)throw new ArgumentException("Expected integer for "+key);return token.Value<int>();}
  static bool Flag(JObject value,string key,bool fallback){var token=value[key];if(token==null)return fallback;if(token.Type!=JTokenType.Boolean)throw new ArgumentException("Expected boolean for "+key);return token.Value<bool>();}
  public static CatalogLoadResult Load(string json,IPrefabCatalog prefabs){
   if(prefabs==null)throw new ArgumentNullException(nameof(prefabs));var errors=new List<string>();var entries=new List<PlantDefinition>();var ids=new HashSet<string>(StringComparer.Ordinal);
   try{
    if(json==null||json.Length>1048576)throw new ArgumentException("Catalog size exceeded");
    JObject root;using(var text=new StringReader(json))using(var reader=new JsonTextReader(text){MaxDepth=32,FloatParseHandling=FloatParseHandling.Decimal}){root=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});if(reader.Read())throw new ArgumentException("Trailing catalog content");}
    if(Integer(root,"schema",-1)!=1)throw new ArgumentException("Unsupported catalog schema");
    if(!(root["entries"] is JArray list)||list.Count>4096)throw new ArgumentException("Invalid catalog entry count");
    foreach(var token in list){
     try{
      if(!(token is JObject obj))throw new ArgumentException("Entry must be an object");
      string id=Text(obj,"id"),prefab=Text(obj,"prefab"),grown=Text(obj,"grownPrefab"),harvest=Text(obj,"harvestItem");
      if(string.IsNullOrWhiteSpace(id)||id.Length>128||!ids.Add(id))throw new ArgumentException("Missing, oversized or duplicate id");
      if(!prefabs.HasPrefab(prefab)||(!string.IsNullOrEmpty(grown)&&!prefabs.HasPrefab(grown)))throw new ArgumentException("Unknown prefab");
      if(!Enum.TryParse<PlantKind>(Text(obj,"kind"),false,out var kind)||!Enum.IsDefined(typeof(PlantKind),kind))throw new ArgumentException("Unknown plant kind");
      bool enabled=Flag(obj,"enabled",true),free=Flag(obj,"free",false);var costs=new List<ResourceCost>();
      if(!(obj["costs"] is JArray costArray))throw new ArgumentException("Costs must be explicit");
      foreach(var costToken in costArray){if(!(costToken is JObject cost))throw new ArgumentException("Invalid cost");var item=Text(cost,"item");if(!prefabs.HasItem(item))throw new ArgumentException("Unknown material");costs.Add(new ResourceCost(item,Integer(cost,"amount",0)));}
      if(enabled&&costs.Count==0&&!free)throw new ArgumentException("No implicit free planting");
      int yield=Integer(obj,"yield",0);if(yield<0||(!string.IsNullOrEmpty(harvest)&&!prefabs.HasItem(harvest))||(yield>0&&string.IsNullOrEmpty(harvest)))throw new ArgumentException("Invalid harvest");
      double min=Number(obj,"growMin",0),max=Number(obj,"growMax",min),respawn=Number(obj,"respawnSeconds",0),radius=Number(obj,"radius",1),scaleMin=Number(obj,"scaleMin",1),scaleMax=Number(obj,"scaleMax",scaleMin);
      if(min<0||max<min||respawn<0||radius<0||scaleMin<=0||scaleMax<scaleMin)throw new ArgumentException("Invalid growth, scale or spacing range");
      entries.Add(new PlantDefinition(id,prefab,grown,kind,costs,harvest,yield,min,max,respawn,radius,scaleMin,scaleMax,enabled,free));
     }catch(Exception error)when(error is ArgumentException||error is OverflowException||error is FormatException){errors.Add("Entry "+entries.Count+": "+error.Message);}
    }
   }catch(Exception error)when(error is JsonException||error is ArgumentException||error is OverflowException||error is FormatException){errors.Add(error.Message);}
   return new CatalogLoadResult(errors.Count==0?new CatalogSnapshot(entries):null,errors);
  }
 }
}
