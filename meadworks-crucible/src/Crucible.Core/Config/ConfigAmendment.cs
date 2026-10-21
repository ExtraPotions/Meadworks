using System;
using System.Collections.Generic;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Where a change to a setting goes (C01 native wiring over Jotunn's synchronized entries).</summary>
    public enum AmendRoute {
        /// <summary>The gate blocks the change; nothing is written.</summary>
        Refused,
        /// <summary>This machine's own file: menu, single player, local settings, unlocked or vanilla-server settings.</summary>
        Own,
        /// <summary>This machine is the authority (host or dedicated server): write, then publish to connected clients.</summary>
        Publish,
        /// <summary>A remote administrator amends the server's value; the server applies it and relays it to everyone.</summary>
        Server,
    }

    /// <summary>
    /// The C01 amendment rule (Charter "steward" model): authority roles write and publish their own value, a remote
    /// administrator sends an amendment to the server, everybody else is refused with the gate's reason.
    /// </summary>
    public static class ConfigAmendment {
        /// <summary>One route for a complete preflight. A single refusal blocks the batch.</summary>
        public static AmendRoute BatchRoute(SessionRole role,IReadOnlyList<AuthorityDecision> decisions){
            if(decisions==null)throw new ArgumentNullException(nameof(decisions));
            if(decisions.Count==0)throw new ArgumentException("An amendment batch needs at least one entry.",nameof(decisions));
            var result=AmendRoute.Own;
            foreach(var decision in decisions){
                var route=Route(role,decision);
                if(route==AmendRoute.Refused)return route;
                if(route==AmendRoute.Server||route==AmendRoute.Publish&&result==AmendRoute.Own)result=route;
            }
            return result;
        }
        public static AmendRoute Route(SessionRole role,AuthorityDecision decision){
            if(decision==null)throw new ArgumentNullException(nameof(decision));
            if(!Enum.IsDefined(typeof(SessionRole),role))throw new ArgumentException("Unknown session role.",nameof(role));
            if(!decision.Allowed)return AmendRoute.Refused;
            if(decision.RequiresServerAmendment)return AmendRoute.Server;
            if(decision.Source!=ConfigValueSource.Own)return AmendRoute.Refused;
            if((role==SessionRole.Host||role==SessionRole.DedicatedServer)&&decision.Scope!=ConfigScope.Local)return AmendRoute.Publish;
            return AmendRoute.Own;
        }
    }

    /// <summary>
    /// Effective values over synchronized entries: a synchronized entry already holds the server's value in memory
    /// (Jotunn restores the player's own value on disconnect), so only a Locked source changes what is read: the safe
    /// value, used for cheats while authority is pending, on vanilla servers and for turned-off products.
    /// </summary>
    public static class ConfigValues {
        public static T Resolve<T>(ConfigValueSource source,T current,T safe){
            switch(source){
                case ConfigValueSource.Own:case ConfigValueSource.Server:return current;
                case ConfigValueSource.Locked:return safe;
                default:throw new ArgumentException("Unknown value source.",nameof(source));
            }
        }
        public static T Resolve<T>(AuthorityDecision decision,T current,T safe)=>Resolve((decision??throw new ArgumentNullException(nameof(decision))).Source,current,safe);
    }
}
