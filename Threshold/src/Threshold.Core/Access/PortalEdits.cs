// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the server's decision for a portal edit (T12b, design TH05): the access mode cycle (Alt + E, U6)
// and hub edits (B1 to B5, Q1, U7). Behavior as Wayfare's ModeCycle (GPL-3.0-or-later, the reference): the request goes
// to the server, which resolves who asked, checks the right with the adapted PortalAccessRules and computes the next mode
// itself, and the cycling player becomes the owner. Hubs follow HubRules. The plan lists the ZDO writes; the plugin
// writes where the server owns the ZDO or nobody does, and forwards the rest to the ZDO's owner.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Access
{
    public enum PortalEditKind { CycleMode, SetHub, ClearHub, SetTag }

    /// <summary>Why the server refused an edit. The hub reasons have the same names as <see cref="HubRefusal"/>.</summary>
    public enum EditRefusal { None, UnknownPortal, UnknownPlayer, ModesOff, NotOwner, NotCreatorOrAdmin, NotAdmin, LimitReached, NameTaken, EmptyName, TaggedPortal, TagLocked }

    public readonly struct PortalEdit
    {
        private PortalEdit(PortalEditKind kind, PortalId portal, string hubName, string author = "")
        {
            Kind = kind;
            Portal = portal;
            HubName = hubName ?? "";
            Author = author ?? "";
        }

        public PortalEditKind Kind { get; }
        public PortalId Portal { get; }

        /// <summary>The requested hub name (SetHub) or tag (SetTag), as typed.</summary>
        public string HubName { get; }

        /// <summary>SetTag only: the game's tag author id (the asking client's platform user id), stored as the game does.</summary>
        public string Author { get; }

        public static PortalEdit Cycle(PortalId portal) => new PortalEdit(PortalEditKind.CycleMode, portal, "");
        public static PortalEdit SetHub(PortalId portal, string name) => new PortalEdit(PortalEditKind.SetHub, portal, name);
        public static PortalEdit ClearHub(PortalId portal) => new PortalEdit(PortalEditKind.ClearHub, portal, "");

        /// <summary>A tag change on a Private or Admin portal (user decision 7 of the runtime questions).</summary>
        public static PortalEdit SetTag(PortalId portal, string tag, string author) => new PortalEdit(PortalEditKind.SetTag, portal, tag, author);

        internal static PortalEdit Of(PortalEditKind kind, PortalId portal, string name, string author) => new PortalEdit(kind, portal, name, author);
    }

    /// <summary>What the server knows when it decides: the target as its own list has it (null when unknown), the whole
    /// list (for hubs), the asking player resolved by the server (0 when unknown) and admin right, the piece creator of
    /// the target, and the server's settings.</summary>
    public sealed class EditFacts
    {
        public EditFacts(PortalEntry? target, IReadOnlyList<PortalEntry> world, long actorId, bool actorIsAdmin, long creator,
            bool accessModesEnabled, HubEditing hubEditing, int maxHubs)
        {
            Target = target;
            World = world ?? Array.Empty<PortalEntry>();
            ActorId = actorId;
            ActorIsAdmin = actorIsAdmin;
            Creator = creator;
            AccessModesEnabled = accessModesEnabled;
            HubEditing = hubEditing;
            MaxHubs = maxHubs;
        }

        public PortalEntry? Target { get; }
        public IReadOnlyList<PortalEntry> World { get; }
        public long ActorId { get; }
        public bool ActorIsAdmin { get; }
        public long Creator { get; }
        public bool AccessModesEnabled { get; }
        public HubEditing HubEditing { get; }
        public int MaxHubs { get; }
    }

    /// <summary>One planned ZDO write: only the fields that are set change.</summary>
    public sealed class PortalWrite
    {
        internal PortalWrite(PortalId portal, AccessMode? mode, long? owner, string? hub, string? tag = null, string? tagAuthor = null)
        {
            Portal = portal;
            Mode = mode;
            Owner = owner;
            Hub = hub;
            Tag = tag;
            TagAuthor = tagAuthor;
        }

        public PortalId Portal { get; }
        public AccessMode? Mode { get; }
        public long? Owner { get; }
        public string? Hub { get; }

        /// <summary>The game's tag and its author id, written together (null: unchanged).</summary>
        public string? Tag { get; }
        public string? TagAuthor { get; }
    }

    public sealed class EditOutcome
    {
        internal EditOutcome(EditRefusal refusal, int argument, IReadOnlyList<PortalWrite> writes)
        {
            Refusal = refusal;
            Argument = argument;
            Writes = writes;
        }

        public EditRefusal Refusal { get; }

        /// <summary>The hub limit for <see cref="EditRefusal.LimitReached"/>, else 0.</summary>
        public int Argument { get; }

        public IReadOnlyList<PortalWrite> Writes { get; }
        public bool Allowed => Refusal == EditRefusal.None;
    }

    public static class PortalEdits
    {
        public static EditOutcome Decide(PortalEdit edit, EditFacts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var none = Array.Empty<PortalWrite>();
            var target = facts.Target;
            if (target == null || !target.Id.Equals(edit.Portal)) return Refuse(EditRefusal.UnknownPortal);
            if (facts.ActorId == 0L) return Refuse(EditRefusal.UnknownPlayer);
            var viewer = new AccessViewer(facts.ActorId, facts.ActorIsAdmin);
            switch (edit.Kind)
            {
                case PortalEditKind.CycleMode:
                {
                    if (!facts.AccessModesEnabled) return Refuse(EditRefusal.ModesOff);
                    if (!PortalAccessRules.MayCycle(target.Mode, target.Owner, viewer)) return Refuse(EditRefusal.NotOwner);
                    var next = PortalAccessRules.NextMode(target.Mode, viewer);
                    if (!PortalAccessRules.MaySet(next, viewer)) return Refuse(EditRefusal.NotOwner);
                    // An unowned portal is claimed by whoever cycles it; an owned one keeps its owner, also when an admin
                    // cycles it (user decision 5 of the runtime questions, a change from Wayfare's rule).
                    long owner = target.Owner != 0L ? target.Owner : facts.ActorId;
                    return new EditOutcome(EditRefusal.None, 0, new[] { new PortalWrite(target.Id, next, owner, null) });
                }
                case PortalEditKind.SetHub:
                {
                    var right = HubRules.MayEdit(facts.HubEditing, facts.Creator, new HubEditor(facts.ActorId, facts.ActorIsAdmin));
                    if (right != HubRefusal.None) return Refuse(Map(right));
                    var slots = facts.World.Where(p => p.IsHub).Select(p => new HubSlot(PortalKey.Of(p.Position), p.HubName)).ToList();
                    var name = HubRules.NormalizeName(edit.HubName);
                    var plan = HubRules.PlanSet(slots, new HubSlot(PortalKey.Of(target.Position), name), facts.MaxHubs, target.IsTagged);
                    if (plan.Refusal != HubRefusal.None)
                        return new EditOutcome(Map(plan.Refusal), plan.Refusal == HubRefusal.LimitReached ? ThresholdSettings.Hubs.MaxHubs.Normalize(facts.MaxHubs) : 0, none);
                    var writes = new List<PortalWrite> { new PortalWrite(target.Id, null, null, name) };
                    foreach (var key in plan.Clear)
                        foreach (var hub in facts.World.Where(p => p.IsHub && PortalKey.Of(p.Position) == key))
                            writes.Add(new PortalWrite(hub.Id, null, null, ""));
                    return new EditOutcome(EditRefusal.None, 0, writes);
                }
                case PortalEditKind.ClearHub:
                {
                    var right = HubRules.MayEdit(facts.HubEditing, facts.Creator, new HubEditor(facts.ActorId, facts.ActorIsAdmin));
                    if (right != HubRefusal.None) return Refuse(Map(right));
                    return new EditOutcome(EditRefusal.None, 0, target.IsHub ? new[] { new PortalWrite(target.Id, null, null, "") } : none);
                }
                case PortalEditKind.SetTag:
                {
                    if (!MayRetag(target.Mode, target.Owner, viewer, facts.AccessModesEnabled)) return Refuse(EditRefusal.TagLocked);
                    return new EditOutcome(EditRefusal.None, 0, new[] { new PortalWrite(target.Id, null, null, null, edit.HubName, edit.Author) });
                }
                default:
                    return Refuse(EditRefusal.UnknownPortal);
            }
        }

        /// <summary>Who may change a portal's tag: anyone on a Public portal (and on every portal while access modes are
        /// off); only the owner or an admin on a Private or Admin portal (user decision 7 of the runtime questions).</summary>
        public static bool MayRetag(AccessMode mode, long owner, AccessViewer viewer, bool modesEnabled)
        {
            if (PortalAccessRules.EffectiveMode(mode, modesEnabled) == AccessMode.Public) return true;
            return viewer.IsAdmin || (owner != 0L && owner == viewer.PlayerId);
        }

        public static string TextKey(EditRefusal refusal) => ThresholdTextKeys.Refusal(refusal);

        private static EditOutcome Refuse(EditRefusal refusal) => new EditOutcome(refusal, 0, Array.Empty<PortalWrite>());

        private static EditRefusal Map(HubRefusal refusal) =>
            Enum.TryParse<EditRefusal>(refusal.ToString(), false, out var mapped) ? mapped : EditRefusal.NotAdmin;
    }

    /// <summary>The server's answer to an edit: the refusal (None when done) and its argument.</summary>
    public readonly struct EditReply
    {
        public EditReply(EditRefusal refusal, int argument)
        {
            Refusal = refusal;
            Argument = argument;
        }

        public EditRefusal Refusal { get; }
        public int Argument { get; }
    }

    public static class PortalEditCodec
    {
        private const byte Format = 2;
        private const int MaxAuthor = 64;

        private static string Cut(string? text, int max)
        {
            var value = text ?? "";
            return value.Length > max ? value.Substring(0, max) : value;
        }
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] EncodeRequest(PortalEdit edit)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Format);
                writer.Write((byte)edit.Kind);
                writer.Write(edit.Portal.User);
                writer.Write(edit.Portal.Id);
                var name = Utf8.GetBytes(HubRules.NormalizeName(edit.HubName));
                writer.Write((byte)name.Length);
                writer.Write(name);
                var author = Utf8.GetBytes(Cut(edit.Author, MaxAuthor));
                writer.Write((byte)author.Length);
                writer.Write(author);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static PortalEdit DecodeRequest(byte[] bytes) => Read(bytes, reader =>
        {
            if (reader.ReadByte() != Format) throw new FormatException("Unknown edit format.");
            var kind = reader.ReadByte();
            if (!Enum.IsDefined(typeof(PortalEditKind), (int)kind)) throw new FormatException("Unknown edit.");
            var id = new PortalId(reader.ReadInt64(), reader.ReadUInt32());
            int length = reader.ReadByte();
            var name = reader.ReadBytes(length);
            if (name.Length != length) throw new FormatException("Truncated name.");
            int authorLength = reader.ReadByte();
            var author = reader.ReadBytes(authorLength);
            if (author.Length != authorLength) throw new FormatException("Truncated author.");
            return PortalEdit.Of((PortalEditKind)kind, id, Utf8.GetString(name), Utf8.GetString(author));
        });

        public static byte[] EncodeReply(EditReply reply)
        {
            var bytes = new byte[5];
            bytes[0] = (byte)reply.Refusal;
            BitConverter.GetBytes(reply.Argument).CopyTo(bytes, 1);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes, 1, 4);
            return bytes;
        }

        public static EditReply DecodeReply(byte[] bytes) => Read(bytes, reader =>
        {
            var refusal = reader.ReadByte();
            if (!Enum.IsDefined(typeof(EditRefusal), (int)refusal)) throw new FormatException("Unknown refusal.");
            return new EditReply((EditRefusal)refusal, reader.ReadInt32());
        });

        private static T Read<T>(byte[] bytes, Func<BinaryReader, T> read)
        {
            if (bytes == null) throw new FormatException("No bytes.");
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream))
                {
                    var value = read(reader);
                    if (stream.Position != stream.Length) throw new FormatException("Trailing bytes.");
                    return value;
                }
            }
            catch (EndOfStreamException e) { throw new FormatException("Truncated.", e); }
            catch (ArgumentException e) { throw new FormatException("Malformed.", e); }
        }
    }

    /// <summary>What a newly placed portal gets (user decision 2 of the runtime questions): the server's Default Access
    /// Mode, written once by the placing client (the new ZDO's owner); a Private or Admin default takes the builder as
    /// owner so the portal stays usable. Existing portals are never touched.</summary>
    public static class PortalPlacement
    {
        /// <summary>The write for a new portal, or null (access modes off, or no builder).</summary>
        public static PortalWrite? Fields(AccessMode defaultMode, long creator, bool modesEnabled)
        {
            if (!modesEnabled || creator == 0L) return null;
            var mode = Enum.IsDefined(typeof(AccessMode), defaultMode) ? defaultMode : AccessMode.Public;
            return new PortalWrite(default, mode, mode == AccessMode.Public ? (long?)null : creator, null);
        }

        /// <summary>Only a placement (the game set the creator just now) of a portal without a stored mode.</summary>
        public static bool Applies(bool placedNow, int storedMode) => placedNow && storedMode == PortalFields.NoMode;
    }
}
