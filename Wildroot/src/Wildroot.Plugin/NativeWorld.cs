namespace ExtraPotions.Wildroot.Runtime {
 /// <summary>The only native world identity read in Wildroot. ZNet.GetWorldUID
 /// dereferences the static loaded world and throws in the main menu, while
 /// connecting and during disconnect; this returns 0 there instead.</summary>
 internal static class NativeWorld {
  internal static long CurrentUid(){ZNet? net=ZNet.instance;if(net==null||!net)return WorldIdentity.Resolve(false,false,()=>0);return WorldIdentity.Resolve(true,net.GetWorld()!=null,()=>net.GetWorldUID());}
  internal static bool IsCurrent(long captured)=>WorldIdentity.IsCurrent(CurrentUid(),captured);
 }
}
