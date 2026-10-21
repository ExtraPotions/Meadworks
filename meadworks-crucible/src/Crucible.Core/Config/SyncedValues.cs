using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>Who changed a synchronized entry on this machine.</summary>
    public enum SyncedWriter {
        /// <summary>Not a connected client, or the entry is not server-bound: nothing is enforced or recorded.</summary>
        Inactive,
        /// <summary>The synchronization layer applying the server's values (or this client's own published amendment).</summary>
        Server,
        /// <summary>A server administrator on this client: an amendment the server accepts, recorded as the server value.</summary>
        Administrator,
        /// <summary>Anything else on a non-administrator client: a file reload, another mod, a configuration UI.</summary>
        Player
    }

    /// <summary>What <see cref="SyncedValueLedger.Observe"/> decided.</summary>
    public enum SyncedChange { Accepted, Recorded, Restore }

    /// <summary>
    /// The server's values for synchronized entries on a connected client (C01). Jotunn writes the server's value into
    /// the entry when it synchronizes, but nothing stops a later local write (another mod, a configuration UI, code)
    /// from replacing it on this client only. The ledger remembers the last server value per entry and asks for it to be
    /// written back when a non-administrator changes the entry. Keys are compared by reference. Main thread only.
    /// </summary>
    public sealed class SyncedValueLedger {
        sealed class ByReference:IEqualityComparer<object> {
            public new bool Equals(object x,object y)=>ReferenceEquals(x,y);
            public int GetHashCode(object obj)=>RuntimeHelpers.GetHashCode(obj);
        }
        readonly Dictionary<object,object?> server=new Dictionary<object,object?>(new ByReference());

        public int Count=>server.Count;

        /// <summary>Records <paramref name="value"/> as the server's; true when it differs from what was recorded.</summary>
        public bool Record(object key,object? value){
            if(key==null)throw new ArgumentNullException(nameof(key));
            if(server.TryGetValue(key,out var known)&&Equals(known,value))return false;
            server[key]=value;return true;
        }

        public bool TryGetServerValue(object key,out object? value)=>server.TryGetValue(key??throw new ArgumentNullException(nameof(key)),out value);

        /// <summary>
        /// One change of <paramref name="key"/> to <paramref name="value"/>. Server and administrator writes are recorded;
        /// a player write that differs from the recorded server value returns <see cref="SyncedChange.Restore"/> with
        /// the value to write back. Without a recorded value (not synchronized yet) the write stands.
        /// </summary>
        public SyncedChange Observe(object key,object? value,SyncedWriter writer,out object? restore){
            if(key==null)throw new ArgumentNullException(nameof(key));
            restore=null;
            switch(writer){
                case SyncedWriter.Inactive:return SyncedChange.Accepted;
                case SyncedWriter.Server:case SyncedWriter.Administrator:server[key]=value;return SyncedChange.Recorded;
                case SyncedWriter.Player:
                    if(!server.TryGetValue(key,out var known)||Equals(known,value))return SyncedChange.Accepted;
                    restore=known;return SyncedChange.Restore;
                default:throw new ArgumentException("Unknown writer.",nameof(writer));
            }
        }

        /// <summary>The connection ended: the player's own values apply again.</summary>
        public void Forget()=>server.Clear();
    }
}
