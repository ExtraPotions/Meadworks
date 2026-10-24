using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Read-only ownership facts from Bind/Track. Query again before editing; authority is always live.</summary>
    public sealed class ConfigEntryMetadata {
        public string ProductId { get; }
        public ConfigScope Scope { get; }
        public object? SafeValue { get; }
        public bool ServerLocked { get; }
        /// <summary>Hidden for entries bound with <see cref="MeadworksConfig.BindHidden{T}"/>; configuration UIs do not list them.</summary>
        public ConfigVisibility Visibility { get; }
        public AuthorityDecision Decision=>MeadworksAuthority.Decide(ProductId,Scope,ServerLocked);
        internal ConfigEntryMetadata(string product,ConfigScope scope,object? safe,bool locked,ConfigVisibility visibility=ConfigVisibility.Shown){ProductId=product;Scope=scope;SafeValue=safe;ServerLocked=locked;Visibility=visibility;}
    }

    public static partial class MeadworksConfig {
        // Ephemeron keys: neither this table nor metadata snapshots retain a ConfigEntry or its ConfigFile.
        static readonly ConditionalWeakTable<ConfigEntryBase,ManagedEntry> managedEntries=new ConditionalWeakTable<ConfigEntryBase,ManagedEntry>();
        static readonly MethodInfo TypedAmend=typeof(MeadworksConfig).GetMethods(BindingFlags.Public|BindingFlags.Static)
            .Single(method=>method.Name==nameof(Amend)&&method.IsGenericMethodDefinition&&method.GetGenericArguments().Length==1&&method.GetParameters().Length==6);
        sealed class ManagedEntry {
            internal bool Bound,Tracked;
            internal string BoundProduct="",Product="";
            internal ConfigScope BoundScope,Scope;
            internal object? Safe;
            internal bool ServerLocked=true;
            internal ConfigVisibility Visibility;
        }
        internal static void RecordBinding(ConfigEntryBase entry,string product,ConfigScope scope,object? safe,ConfigVisibility visibility=ConfigVisibility.Shown){
            var data=managedEntries.GetValue(entry,_=>new ManagedEntry());
            data.Bound=true;data.BoundProduct=product;data.BoundScope=scope;data.Safe=safe;data.Visibility=visibility;
            if(!data.Tracked){data.Product=product;data.Scope=scope;data.ServerLocked=true;}
            if(scope!=ConfigScope.Local)SyncedConfigGuard.Watch(entry);
        }
        internal static void RecordTracking(ConfigEntryBase entry,string product,ConfigScope scope,bool serverLocked){
            if(string.IsNullOrWhiteSpace(product)||!Enum.IsDefined(typeof(ConfigScope),scope))return;
            var data=managedEntries.GetValue(entry,_=>new ManagedEntry{Safe=entry.DefaultValue});
            data.Tracked=true;data.Product=product;data.Scope=scope;data.ServerLocked=serverLocked;
        }
        internal static void EndTracking(ConfigEntryBase entry){
            if(entry==null||!managedEntries.TryGetValue(entry,out var data))return;
            if(!data.Bound){managedEntries.Remove(entry);return;}
            data.Tracked=false;data.Product=data.BoundProduct;data.Scope=data.BoundScope;data.ServerLocked=true;
        }
        /// <summary>Only exact entries registered by this runtime's Bind or active Track are managed.</summary>
        public static bool TryGetMetadata(ConfigEntryBase entry,out ConfigEntryMetadata metadata){
            if(entry!=null&&managedEntries.TryGetValue(entry,out var data)&&!string.IsNullOrWhiteSpace(data.Product)){
                metadata=new ConfigEntryMetadata(data.Product,data.Scope,data.Safe,data.ServerLocked,data.Visibility);return true;
            }
            metadata=null!;return false;
        }
        /// <summary>Non-generic editor seam through the same typed amendment and Jotunn route. No coercion or direct write.</summary>
        public static ConfigAmendResult Amend(ConfigEntryBase entry,object? value,string? label=null){
            using var timing=EditTiming.Begin("Amend");
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            if(!TryGetMetadata(entry,out var metadata))throw new InvalidOperationException("No registered Meadworks owner for this configuration entry.");
            var type=entry.SettingType;
            if(entry.GetType()!=typeof(ConfigEntry<>).MakeGenericType(type))throw new ArgumentException("Expected the exact registered BepInEx configuration entry type.",nameof(entry));
            if(value==null?type.IsValueType&&Nullable.GetUnderlyingType(type)==null:!type.IsInstanceOfType(value))
                throw new ArgumentException("The value must have the entry's exact managed setting type.",nameof(value));
            try{var result=(ConfigAmendResult)TypedAmend.MakeGenericMethod(type).Invoke(null,new object?[]{entry,metadata.ProductId,metadata.Scope,value,label,metadata.ServerLocked})!;timing.Success();return result;}
            catch(TargetInvocationException error) when(error.InnerException!=null){ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
    }
}
