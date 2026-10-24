// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace Burrow.SharedTransfers {
    internal sealed class OwnerImportSnapshot<TId> {
        readonly object source;
        readonly long world,owner;
        readonly ushort ownerRevision;
        readonly TId nativeId;
        readonly byte[] migration,identity;
        internal OwnerImportSnapshot(object source,long world,long owner,ushort ownerRevision,TId nativeId,byte[]? migration,byte[]? identity){
            this.source=source;this.world=world;this.owner=owner;this.ownerRevision=ownerRevision;this.nativeId=nativeId;
            this.migration=Copy(migration);this.identity=Copy(identity);
        }
        internal bool Matches(object source,long world,long owner,ushort ownerRevision,TId nativeId,byte[]? migration,byte[]? identity)=>
            ReferenceEquals(this.source,source)&&this.world==world&&this.owner==owner&&this.ownerRevision==ownerRevision&&
            EqualityComparer<TId>.Default.Equals(this.nativeId,nativeId)&&Same(this.migration,migration)&&Same(this.identity,identity);
        static byte[] Copy(byte[]? bytes)=>bytes==null||bytes.Length==0?Array.Empty<byte>():(byte[])bytes.Clone();
        static bool Same(byte[] before,byte[]? current){
            if(current==null)return before.Length==0;
            int count=current.Length;if(before.Length!=count)return false;
            for(int index=0;index<count;index++)if(before[index]!=current[index])return false;
            return true;
        }
    }
}
