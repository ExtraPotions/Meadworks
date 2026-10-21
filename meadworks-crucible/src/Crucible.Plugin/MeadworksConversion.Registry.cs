// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Product-owned conversion callbacks. The product validates preview identity and current write authority at Apply.</summary>
    public sealed class ConversionRegistration {
        public string Id { get; }
        public string ProductId { get; }
        public string SourceId { get; }
        public string SourceName { get; }
        public Func<bool> Available { get; }
        public Func<object> Preview { get; }
        public Func<object,IReadOnlyDictionary<string,string>,bool,object> Apply { get; }
        public ConversionRegistration(string id,string productId,string sourceId,string sourceName,Func<bool> available,Func<object> preview,Func<object,IReadOnlyDictionary<string,string>,bool,object> apply){
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(productId)||string.IsNullOrWhiteSpace(sourceId))throw new ArgumentException("Conversion identities required.");
            Id=id;ProductId=productId;SourceId=sourceId;SourceName=sourceName??sourceId;Available=available??throw new ArgumentNullException(nameof(available));Preview=preview??throw new ArgumentNullException(nameof(preview));Apply=apply??throw new ArgumentNullException(nameof(apply));
        }
    }
    public static partial class MeadworksConversion {
        static readonly object registryGate=new object();
        static readonly Dictionary<string,ConversionRegistration> registrations=new Dictionary<string,ConversionRegistration>(StringComparer.Ordinal);
        public static IReadOnlyList<ConversionRegistration> Registered { get {lock(registryGate)return registrations.Values.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray();} }
        public static IDisposable Register(ConversionRegistration item){if(item==null)throw new ArgumentNullException(nameof(item));lock(registryGate){if(registrations.ContainsKey(item.Id))throw new InvalidOperationException("Conversion already registered: "+item.Id);registrations.Add(item.Id,item);}return new Registration(()=>{lock(registryGate){if(registrations.TryGetValue(item.Id,out var current)&&ReferenceEquals(current,item))registrations.Remove(item.Id);}});}
        sealed class Registration:IDisposable {Action? remove;internal Registration(Action remove){this.remove=remove;}public void Dispose(){var action=System.Threading.Interlocked.Exchange(ref remove,null);action?.Invoke();}}
    }
}
