using System;
namespace ExtraPotions.Wildroot {
 /// <summary>Keeps a built value until the caller's revision number changes. For per-frame readers of configuration that changes rarely.</summary>
 public sealed class RevisionCache<T> where T:class {
  T? value;int revision=int.MinValue;
  public T Get(int current,Func<T> build){if(value==null||revision!=current){value=build();revision=current;}return value;}
 }
}
