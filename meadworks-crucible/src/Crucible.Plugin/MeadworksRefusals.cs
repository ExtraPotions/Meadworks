using System;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The client's last C04 peer-class refusal (<see cref="RefusalInbox"/>), kept until a screen shows it: Crucible.UI's
    /// refusal screen at the main menu's connection-failed moment, behind the <c>Network.PeerClassHandshake</c> flag.
    /// Crucible itself has no UI; it records the verdict the client computed from the server's product set and the
    /// server's <see cref="RefusalNotice"/> (its refusal code, also in the server's log).
    /// </summary>
    public static class MeadworksRefusals {
        static RefusalNotice? lastNotice;
        public static RefusalInbox Inbox { get; }=new RefusalInbox();
        public static PeerClassVerdict? Pending=>Inbox.Pending;
        /// <summary>The server's last refusal notice for this client; null until a server refuses it.</summary>
        public static RefusalNotice? LastNotice=>lastNotice;
        /// <summary>The refusal code of <see cref="LastNotice"/>, the one the server's log carries.</summary>
        public static string? LastCode=>lastNotice?.Code;
        public static void Record(PeerClassVerdict verdict)=>Inbox.Record(verdict);
        public static void RecordNotice(RefusalNotice notice)=>lastNotice=notice??throw new ArgumentNullException(nameof(notice));
        public static PeerClassVerdict? Take()=>Inbox.Take();
        public static void Clear(){Inbox.Clear();lastNotice=null;}
    }
}
