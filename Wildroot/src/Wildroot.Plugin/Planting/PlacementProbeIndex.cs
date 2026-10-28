using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Wildroot.Runtime {
 internal static class PlacementProbeIndex {
  static readonly int RequestKey="wildroot.request".GetStableHashCode();
  static Cache? current;
  static int depth;
  static int queries;
  static long revision;

  internal struct Batch:IDisposable {
   readonly Cache? previous;
   readonly int level;
   readonly int previousQueries;
   bool disposed;
   internal Batch(bool begin){previous=current;previousQueries=queries;level=++depth;current=null;queries=0;disposed=false;}
   public void Dispose(){if(disposed)return;if(depth!=level)throw new InvalidOperationException("Placement probe scopes must close in order.");disposed=true;current=previous;queries=previousQueries;depth--;}
  }
  internal struct Mutation:IDisposable {
   internal Mutation(bool begin){Invalidate();}
   public void Dispose()=>Invalidate();
  }
  internal static Batch Begin()=>new Batch(true);
  internal static Mutation Changing()=>new Mutation(true);
  internal static void Invalidate(){if(depth>0)unchecked{revision++;}}

  internal static ZNetView[] Select(string request,Func<IEnumerable<ZNetView>,ZNetView[]> select){
   var instances=PlacementBackend.Instances;
   // A lone probe performs two queries; keep that case on its original scan path.
   if(depth==0||++queries<=2)return select(instances.Values);
   current??=new Cache();
   long before=revision;
   int count=instances.Count;
   var candidates=current.Find(instances,request);
   var selected=select(candidates);
   if(before!=revision||count!=instances.Count||!ReferenceEquals(instances,PlacementBackend.Instances))return select(PlacementBackend.Instances.Values);
   return selected;
  }
  sealed class Cache {
   object? identity;
   int count;
   long builtRevision;
   Dictionary<string,List<ZNetView>>? requests;
   internal IEnumerable<ZNetView> Find(Dictionary<ZDO,ZNetView> instances,string request){
    if(requests==null||!ReferenceEquals(identity,instances)||count!=instances.Count||builtRevision!=revision){
     long before=revision;
     var next=new Dictionary<string,List<ZNetView>>(StringComparer.Ordinal);
     foreach(var view in instances.Values){
      if(!view||!view.IsValid())continue;
      var key=view.GetZDO().GetString("wildroot.request");
      if(string.IsNullOrEmpty(key))continue;
      if(!next.TryGetValue(key,out var bucket)){bucket=new List<ZNetView>();next.Add(key,bucket);}
      bucket.Add(view);
     }
     // Native callbacks may change membership while we inspect views. Never publish that partial negative index.
     if(before!=revision||!ReferenceEquals(instances,PlacementBackend.Instances)){requests=null;identity=null;return PlacementBackend.Instances.Values;}
     identity=instances;count=instances.Count;builtRevision=before;requests=next;
    }
    return requests.TryGetValue(request,out var matches)?matches:Array.Empty<ZNetView>();
   }
  }

  [HarmonyPatch(typeof(ZNetView),"Awake")]
  static class SceneViewAwake {
   [HarmonyPrefix,HarmonyPriority(Priority.First)] static void Prefix()=>Invalidate();
   [HarmonyFinalizer] static void Finalizer()=>Invalidate();
  }
  [HarmonyPatch(typeof(ZNetView),"OnDestroy")]
  static class SceneViewDestroy {
   [HarmonyPrefix,HarmonyPriority(Priority.First)] static void Prefix()=>Invalidate();
   [HarmonyFinalizer] static void Finalizer()=>Invalidate();
  }
  [HarmonyPatch(typeof(ZDO),nameof(ZDO.Set),typeof(int),typeof(string))]
  static class RequestWrite {
   [HarmonyPrefix,HarmonyPriority(Priority.First)] static void Prefix(int __0){if(__0==RequestKey)Invalidate();}
   [HarmonyFinalizer] static void Finalizer(int __0){if(__0==RequestKey)Invalidate();}
  }
 }
}
