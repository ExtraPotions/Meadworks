using System;
namespace ExtraPotions.Wildroot {
 /// <summary>Runs a lookup the first time it is asked and keeps the answer, including null. For reflection lookups that would otherwise scan every loaded type each frame.</summary>
 public sealed class ResolveOnce<T> where T:class {
  readonly Func<T?> resolver;T? value;bool done;
  public ResolveOnce(Func<T?> resolver){this.resolver=resolver;}
  public T? Get(){if(!done){value=resolver();done=true;}return value;}
 }
}
