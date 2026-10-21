using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ExtraPotions.Wildroot {
 /// <summary>Restores live component values only while they still belong to this catalog.</summary>
 public sealed class LiveCatalogValues {
  ConditionalWeakTable<object,Dictionary<string,Value>> values=new ConditionalWeakTable<object,Dictionary<string,Value>>();
  readonly List<WeakReference<object>> targets=new List<WeakReference<object>>();
  sealed class Value {
   internal object? Original,Applied;
   internal bool Owned;
   internal Func<object?,object?> Snapshot=Identity;
  }
  static object? Identity(object? value)=>value;
  public bool Apply(object target,string member,object? current,object? original,object? effective,Func<object?,object?>? snapshot=null){
   snapshot=snapshot??Identity;var observed=snapshot(current);
   if(!values.TryGetValue(target,out var fields)){
    fields=new Dictionary<string,Value>(StringComparer.Ordinal);values.Add(target,fields);targets.Add(new WeakReference<object>(target));
   }
   if(!fields.TryGetValue(member,out var value)){
    // New instances copied the effective prefab; older instances still have its original value.
    bool copied=Equals(observed,snapshot(effective));bool owned=Equals(observed,snapshot(original))||copied;
    fields[member]=value=new Value{Original=copied?original:current,Applied=observed,Owned=owned,Snapshot=snapshot};
   }
   if(!value.Owned)return false;
   if(!Equals(observed,value.Applied)){value.Owned=false;return false;}
   value.Applied=snapshot(effective);return true;
  }
  public void Restore(Func<object,bool> alive,Func<object,string,object?> read,Action<object,string,object?> write){
   foreach(var reference in targets){
    if(!reference.TryGetTarget(out var target)||!alive(target)||!values.TryGetValue(target,out var fields))continue;
    foreach(var field in fields){var value=field.Value;if(value.Owned&&Equals(value.Snapshot(read(target,field.Key)),value.Applied))write(target,field.Key,value.Original);}
   }
   targets.Clear();values=new ConditionalWeakTable<object,Dictionary<string,Value>>();
  }
 }
}
