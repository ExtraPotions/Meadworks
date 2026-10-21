using System;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Runs an expensive lookup (reflection over every loaded type, for instance) until it finds something, then keeps
    /// the answer. A null result is not remembered, so a type or member that is not there yet is looked up again on the
    /// next call, exactly as an uncached lookup would; a resolver exception is not caught or remembered. Main thread only.
    /// </summary>
    public sealed class ResolveOnce<T> where T:class {
        readonly Func<T?> resolve;
        T? value;
        public ResolveOnce(Func<T?> resolve){this.resolve=resolve??throw new ArgumentNullException(nameof(resolve));}
        public T? Get(){
            var cached=value;if(cached!=null)return cached;
            return value=resolve();
        }
    }
}
