using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    public static partial class MeadworksConfig {
        /// <summary>
        /// Preflights every exact managed entry before invoking an atomic product write once, with one Jotunn
        /// cache/send pair for synchronized changes. Main thread only. The callback owns validation, rollback and
        /// persistence, must write only the declared entries, and must not invoke another amendment. This is not
        /// an atomic distributed transaction or a helper for persisting remote server overlays into a client file.
        /// </summary>
        public static ConfigAmendResult AmendBatch(string productId,IReadOnlyList<ConfigEntryBase> entries,Action apply,string? label=null){
            if(string.IsNullOrWhiteSpace(productId))throw new ArgumentException("A product owner is required.",nameof(productId));
            if(entries==null)throw new ArgumentNullException(nameof(entries));
            if(apply==null)throw new ArgumentNullException(nameof(apply));
            if(entries.Count==0)throw new ArgumentException("An amendment batch needs at least one entry.",nameof(entries));
            var seen=new HashSet<ConfigEntryBase>();
            var decisions=new List<AuthorityDecision>(entries.Count);
            foreach(var entry in entries){
                if(entry==null||!seen.Add(entry))throw new ArgumentException("A batch needs distinct non-null entries.",nameof(entries));
                if(!TryGetMetadata(entry,out var metadata)||!string.Equals(metadata.ProductId,productId,StringComparison.Ordinal))
                    throw new InvalidOperationException("The batch contains an entry without the exact registered product owner.");
                decisions.Add(metadata.Decision);
            }
            var role=Plugin.IsReady?Plugin.Session.Role:SessionRole.Menu;
            var route=ConfigAmendment.BatchRoute(role,decisions);
            if(route==AmendRoute.Refused){
                for(int i=0;i<decisions.Count;i++)if(ConfigAmendment.Route(role,decisions[i])==AmendRoute.Refused){
                    MeadworksBlockedOptions.Notify(BlockedOption.From(productId,entries[i].Definition.Key,decisions[i]),label);
                    return new ConfigAmendResult(route,decisions[i],false);
                }
            }
            var governing=decisions[0];
            foreach(var decision in decisions)if(ConfigAmendment.Route(role,decision)==route){governing=decision;break;}
            if(route==AmendRoute.Own){apply();return new ConfigAmendResult(route,governing,true);}
            // The single-entry legacy bridge applies locally when unavailable. A batch must fail before its write.
            if(!JotunnConfigBridge.Available){Plugin.Log?.LogWarning("[Meadworks:Crucible] Configuration batch not applied: Jotunn synchronization is unavailable.");return new ConfigAmendResult(route,governing,false);}
            return new ConfigAmendResult(route,governing,JotunnConfigBridge.Publish(apply));
        }
    }
}
