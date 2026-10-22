// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Exact registration/delegate ownership follows the established Edge SubSummonSystem lease pattern.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Runtime {
    internal enum NativeRpcOwner { EliteEffects, Tier, BossCredit, BossBoard, BossRecall, BossHints, Shriek, StealReply, CreatureFlash, CreatureDevour, CreatureBoss, CreatureThieving, CreatureClone, CreatureDecoy, CreatureBlink, CreatureShriek, CreatureBrutal, CreatureColossal, CreatureGravitic, CreaturePhantom, CreatureHowl, CMModifierEffects, CMModifierCharacter, CMKarma, LuckPlayer, TheftStatus, RaidAdmission }

    /// <summary>Product-owned native registration groups; source wire names and callback algorithms stay intact.</summary>
    internal static class NativeRpcOwnership {
        sealed class Write {
            internal int Key;
            internal object BoxedKey=null!; // Boxed once; Matches runs every frame for every owned group
            internal WeakReference<object> Registration=null!;
            internal WeakReference<Delegate> Action=null!;
            internal FieldInfo ActionField=null!;
            internal bool Matches(IDictionary table) => Registration.TryGetTarget(out var registration) && Action.TryGetTarget(out var action)
                && table.Contains(BoxedKey) && ReferenceEquals(table[BoxedKey],registration) && ReferenceEquals(ActionField.GetValue(registration),action);
        }
        sealed class Lease {
            internal NativeRpcOwner Owner;
            internal WeakReference<IDictionary> Table=null!;
            internal Action? Reset;
            internal readonly List<Write> Writes=new List<Write>();
            internal bool Matches(IDictionary table) {
                if(!Table.TryGetTarget(out var prior)||!ReferenceEquals(prior,table))return false;
                foreach(var write in Writes)if(!write.Matches(table))return false;
                return true;
            }
            internal void Dispose() {
                try { if(Table.TryGetTarget(out var table))foreach(var write in Writes)if(write.Matches(table))table.Remove(write.Key); }
                finally { Reset?.Invoke(); }
            }
        }
        static readonly List<Lease> leases=new List<Lease>();
        static readonly Dictionary<NativeRpcOwner,Lease> routedLeases=new Dictionary<NativeRpcOwner,Lease>();
        static ConditionalWeakTable<IDictionary,Dictionary<NativeRpcOwner,Lease>> viewLeases=new ConditionalWeakTable<IDictionary,Dictionary<NativeRpcOwner,Lease>>();
        static bool accepting=true;
        static readonly HashSet<string> warned=new HashSet<string>(StringComparer.Ordinal);

        internal static bool Routed(ZRoutedRpc bus,NativeRpcOwner owner,Action register,Action reset,params string[] names) {
            if(!accepting||bus==null)return false;
            var table=(IDictionary)bus.m_functions;
            routedLeases.TryGetValue(owner,out var prior);
            if(prior!=null&&prior.Matches(table))return true;
            if(prior!=null){leases.Remove(prior);prior.Dispose();}
            return Install(table,owner,register,reset,names);
        }
        internal static bool View(ZNetView view,NativeRpcOwner owner,Action register,params string[] names) {
            if(!accepting||view==null||!view.IsValid())return false;
            var table=(IDictionary)view.m_functions;
            // Dead view tables are weakly held, so callback closures cannot keep their creatures alive.
            leases.RemoveAll(lease=>lease.Reset==null&&!lease.Table.TryGetTarget(out _));
            var groups=viewLeases.GetOrCreateValue(table);
            groups.TryGetValue(owner,out var prior);
            if(prior!=null&&prior.Matches(table))return true;
            if(prior!=null){leases.Remove(prior);prior.Dispose();}
            return Install(table,owner,register,null,names);
        }
        internal static bool OwnsRouted(ZRoutedRpc bus, NativeRpcOwner owner) {
            if(!accepting||bus==null)return false;
            var table=(IDictionary)bus.m_functions;
            return routedLeases.TryGetValue(owner,out var lease)&&lease.Matches(table);
        }
        internal static bool OwnsView(ZNetView view, NativeRpcOwner owner) {
            if(!accepting||view==null||!view.IsValid())return false;
            var table=(IDictionary)view.m_functions;
            return viewLeases.TryGetValue(table,out var groups)&&groups.TryGetValue(owner,out var lease)&&lease.Matches(table);
        }
        static bool Install(IDictionary table,NativeRpcOwner owner,Action register,Action? reset,string[] names) {
            var keys=names.Select(name=>name.GetStableHashCode()).ToArray();
            if(keys.Distinct().Count()!=keys.Length)throw new ArgumentException("Duplicate native RPC name in registration group.");
            for(int index=0;index<keys.Length;index++)if(table.Contains(keys[index])){
                if(warned.Add(owner+":"+names[index]))Log.Warn("Wildblood native transport unavailable: registered name "+names[index]+" is already occupied.");
                reset?.Invoke();return false;
            }
            var lease=new Lease{Owner=owner,Table=new WeakReference<IDictionary>(table),Reset=reset};
            try {
                register();
                foreach(var key in keys){
                    var registration=table[key]??throw new InvalidOperationException("Native RPC registration was not installed.");
                    var field=registration.GetType().GetField("m_action",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)??throw new InvalidOperationException("Native RPC delegate identity is unavailable.");
                    var action=field.GetValue(registration) as Delegate??throw new InvalidOperationException("Native RPC delegate identity is unavailable.");
                    lease.Writes.Add(new Write{Key=key,BoxedKey=key,Registration=new WeakReference<object>(registration),Action=new WeakReference<Delegate>(action),ActionField=field});
                }
                leases.Add(lease);
                if(reset==null)viewLeases.GetOrCreateValue(table)[owner]=lease;else routedLeases[owner]=lease;
                return true;
            } catch {
                // Every declared key was absent before this synchronous group began. Capture any partial writes too.
                foreach(var key in keys.Where(key=>table.Contains(key)&&!lease.Writes.Any(write=>write.Key==key))){
                    var registration=table[key];var field=registration?.GetType().GetField("m_action",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    if(registration!=null&&field?.GetValue(registration) is Delegate action)lease.Writes.Add(new Write{Key=key,BoxedKey=key,Registration=new WeakReference<object>(registration),Action=new WeakReference<Delegate>(action),ActionField=field});
                }
                lease.Dispose();throw;
            }
        }
        internal static void Release(NativeRpcOwner owner) {foreach(var lease in leases.Where(lease=>lease.Owner==owner).ToArray()){leases.Remove(lease);lease.Dispose();}routedLeases.Remove(owner);}
        internal static void Resume() {accepting=true;}
        internal static void Shutdown() {accepting=false;foreach(var lease in leases.ToArray()){leases.Remove(lease);lease.Dispose();}routedLeases.Clear();viewLeases=new ConditionalWeakTable<IDictionary,Dictionary<NativeRpcOwner,Lease>>();warned.Clear();}
    }
}
