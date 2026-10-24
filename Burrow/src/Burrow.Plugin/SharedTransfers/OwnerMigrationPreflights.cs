// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Burrow.SharedTransfers {
    internal sealed class OwnerMigrationPreflights<TId> {
        readonly Dictionary<string,Lease> active=new Dictionary<string,Lease>(StringComparer.Ordinal);
        internal IDisposable Enter(string source,string fingerprint,object chest,OwnerImportSnapshot<TId> baseline,byte[] metadata,byte[] before,byte[] after,byte[] migration){
            active.TryGetValue(source,out var previous);
            var lease=new Lease(this,previous,source,fingerprint,chest,baseline,metadata,before,after,migration);
            active[source]=lease;return lease;
        }
        internal bool TryUse(string source,string fingerprint,object chest,object zdo,long world,long owner,ushort ownerRevision,TId nativeId,byte[]? baselineMigration,byte[]? identity,byte[] metadata,byte[] before,byte[] after,out byte[] migration){
            migration=Array.Empty<byte>();
            if(!active.TryGetValue(source,out var lease)||lease.Fingerprint!=fingerprint||!ReferenceEquals(lease.Chest,chest)||
                !lease.Baseline.Matches(zdo,world,owner,ownerRevision,nativeId,baselineMigration,identity)||
                !Same(lease.Metadata,metadata)||!Same(lease.Before,before)||!Same(lease.After,after))return false;
            migration=(byte[])lease.Migration.Clone();return true;
        }
        static bool Same(byte[] left,byte[] right){
            if(left.Length!=right.Length)return false;
            for(int index=0;index<left.Length;index++)if(left[index]!=right[index])return false;
            return true;
        }
        sealed class Lease:IDisposable {
            readonly OwnerMigrationPreflights<TId> owner;readonly Lease? previous;readonly string source;bool disposed;
            internal readonly string Fingerprint;internal readonly object Chest;internal readonly OwnerImportSnapshot<TId> Baseline;
            internal readonly byte[] Metadata,Before,After,Migration;
            internal Lease(OwnerMigrationPreflights<TId> owner,Lease? previous,string source,string fingerprint,object chest,OwnerImportSnapshot<TId> baseline,byte[] metadata,byte[] before,byte[] after,byte[] migration){
                this.owner=owner;this.previous=previous;this.source=source;Fingerprint=fingerprint;Chest=chest;Baseline=baseline;
                Metadata=(byte[])metadata.Clone();Before=(byte[])before.Clone();After=(byte[])after.Clone();Migration=(byte[])migration.Clone();
            }
            public void Dispose(){
                if(disposed)return;
                // Nested admissions for the same root must restore their caller's preflight.
                if(!owner.active.TryGetValue(source,out var current)||!ReferenceEquals(current,this))throw new InvalidOperationException("Migration preflight scopes must close in order.");
                if(previous==null)owner.active.Remove(source);else owner.active[source]=previous;
                disposed=true;
            }
        }
    }
}
