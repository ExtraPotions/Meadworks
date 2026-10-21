// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the server portal list on the wire (design D1, T11). Behavior as Wayfare's PortalSync
// (GPL-3.0-or-later, read as the reference): only the server's list is complete, because the game sends portal ZDOs only
// for sectors near each peer, so clients ask the server with the version they hold and the server answers only when its
// list differs. Threshold carries the request over a Crucible operation, whose frame is 64 KiB, so a large world is
// sent in pages; the version is Threshold's own content hash (PortalListVersion).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExtraPotions.Threshold.Access;

namespace ExtraPotions.Threshold.Portals
{
    /// <summary>Threshold's data on a portal's own ZDO (the game hashes each key name). Absent values read as unowned,
    /// Public and no hub.</summary>
    public static class PortalFields
    {
        public const string ModeKey = ThresholdCore.ZdoPrefix + "mode";
        public const string OwnerKey = ThresholdCore.ZdoPrefix + "owner";
        public const string HubKey = ThresholdCore.ZdoPrefix + "hub";

        /// <summary>The value the plugin reads when no mode was ever stored.</summary>
        public const int NoMode = -1;

        /// <summary>A stored mode number: absent and unknown values (the retired Guild 3 among them) read as Public (U2).</summary>
        public static AccessMode Mode(int raw) =>
            raw >= 0 && Enum.IsDefined(typeof(AccessMode), raw) ? (AccessMode)raw : AccessMode.Public;
    }

    /// <summary>A client's ask: the version of the complete list it holds (0 for none) and the page it wants.</summary>
    public readonly struct PortalListRequest
    {
        public PortalListRequest(long held, int page)
        {
            Held = held;
            Page = page;
        }

        public long Held { get; }
        public int Page { get; }
    }

    /// <summary>One page of the server's list at <see cref="Version"/>, or the answer that the client's list is current.</summary>
    public sealed class PortalListPage
    {
        public PortalListPage(long version, int page, int pages, IReadOnlyList<PortalEntry> entries)
        {
            if (pages < 1 || page < 0 || page >= pages) throw new ArgumentOutOfRangeException(nameof(page), "Page outside the list.");
            Version = version;
            Page = page;
            Pages = pages;
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        private PortalListPage(long version)
        {
            Version = version;
            Entries = Array.Empty<PortalEntry>();
            IsUnchanged = true;
        }

        public static PortalListPage Unchanged(long version) => new PortalListPage(version);

        public long Version { get; }
        public int Page { get; }
        public int Pages { get; }
        public IReadOnlyList<PortalEntry> Entries { get; }

        /// <summary>The client already holds this version; nothing else is sent.</summary>
        public bool IsUnchanged { get; }
    }

    /// <summary>The bytes of a request and a page. Little-endian; strings as UTF-8 with a length; every read is bounded,
    /// and anything malformed (short, trailing bytes, unknown format, out of range) is a <see cref="FormatException"/>.</summary>
    public static class PortalListCodec
    {
        /// <summary>Largest encoded page: well inside Crucible's 64 KiB operation frame with its envelope.</summary>
        public const int PageBudget = 56 * 1024;

        public const int MaxEntriesPerPage = 4096;
        public const int MaxPages = 256;
        public const int MaxStringBytes = 256;

        private const byte RequestFormat = 1;
        private const byte PageFormat = 1;
        private const byte UnchangedFormat = 2;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] EncodeRequest(PortalListRequest request)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(RequestFormat);
                writer.Write(request.Held);
                writer.Write(request.Page);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static PortalListRequest DecodeRequest(byte[] bytes) => Read(bytes, reader =>
        {
            if (reader.ReadByte() != RequestFormat) throw new FormatException("Unknown request format.");
            var held = reader.ReadInt64();
            var page = reader.ReadInt32();
            if (page < 0 || page >= MaxPages) throw new FormatException("Page out of range.");
            return new PortalListRequest(held, page);
        });

        public static byte[] EncodeReply(PortalListPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                if (page.IsUnchanged)
                {
                    writer.Write(UnchangedFormat);
                    writer.Write(page.Version);
                }
                else
                {
                    writer.Write(PageFormat);
                    writer.Write(page.Version);
                    writer.Write(page.Page);
                    writer.Write(page.Pages);
                    writer.Write(page.Entries.Count);
                    foreach (var entry in page.Entries) WriteEntry(writer, entry);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static PortalListPage DecodeReply(byte[] bytes) => Read(bytes, reader =>
        {
            var format = reader.ReadByte();
            if (format == UnchangedFormat) return PortalListPage.Unchanged(reader.ReadInt64());
            if (format != PageFormat) throw new FormatException("Unknown page format.");
            var version = reader.ReadInt64();
            var page = reader.ReadInt32();
            var pages = reader.ReadInt32();
            if (pages < 1 || pages > MaxPages || page < 0 || page >= pages) throw new FormatException("Page out of range.");
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxEntriesPerPage) throw new FormatException("Entry count out of range.");
            var entries = new List<PortalEntry>(count);
            for (int i = 0; i < count; i++) entries.Add(ReadEntry(reader));
            return new PortalListPage(version, page, pages, entries);
        });

        /// <summary>Encoded size of one entry (the pager's measure).</summary>
        public static int EntrySize(PortalEntry entry)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteEntry(writer, entry);
                writer.Flush();
                return (int)stream.Length;
            }
        }

        /// <summary>Encoded size of a page without entries.</summary>
        public const int PageHeaderSize = 1 + 8 + 4 + 4 + 4;

        private static void WriteEntry(BinaryWriter writer, PortalEntry entry)
        {
            writer.Write(entry.Id.User);
            writer.Write(entry.Id.Id);
            WriteString(writer, entry.Prefab);
            WriteString(writer, entry.Tag);
            writer.Write(entry.Position.X);
            writer.Write(entry.Position.Y);
            writer.Write(entry.Position.Z);
            writer.Write((byte)entry.Mode);
            writer.Write(entry.Owner);
            WriteString(writer, entry.HubName);
            writer.Write(entry.AllowsAllItems);
            writer.Write(entry.Yaw);
        }

        private static PortalEntry ReadEntry(BinaryReader reader)
        {
            var user = reader.ReadInt64();
            var id = reader.ReadUInt32();
            var prefab = ReadString(reader);
            var tag = ReadString(reader);
            var position = new MapPoint(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var mode = PortalFields.Mode(reader.ReadByte());
            var owner = reader.ReadInt64();
            var hub = ReadString(reader);
            var all = reader.ReadByte();
            var yaw = reader.ReadSingle();
            if (all > 1) throw new FormatException("Malformed flag.");
            if (prefab.Length == 0 || !position.IsFinite) throw new FormatException("Malformed portal.");
            return new PortalEntry(new PortalId(user, id), prefab, tag, position, mode, owner, hub, all == 1, yaw);
        }

        /// <summary>Strings longer than <see cref="MaxStringBytes"/> are cut at a character boundary.</summary>
        private static void WriteString(BinaryWriter writer, string text)
        {
            var value = text ?? "";
            var bytes = Utf8.GetBytes(value);
            while (bytes.Length > MaxStringBytes)
            {
                value = value.Substring(0, value.Length - 1);
                if (value.Length > 0 && char.IsHighSurrogate(value[value.Length - 1])) value = value.Substring(0, value.Length - 1);
                bytes = Utf8.GetBytes(value);
            }
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            if (length > MaxStringBytes) throw new FormatException("String too long.");
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new FormatException("Truncated string.");
            try { return Utf8.GetString(bytes); }
            catch (ArgumentException e) { throw new FormatException("Malformed text.", e); }
        }

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
            catch (ArgumentOutOfRangeException e) { throw new FormatException("Out of range.", e); }
            catch (ArgumentException e) { throw new FormatException("Malformed.", e); }
        }
    }

    public static class PortalListPager
    {
        /// <summary>The list in order, cut into pages whose encoded size stays within <paramref name="budget"/>; always at
        /// least one page (empty for an empty world).</summary>
        public static IReadOnlyList<IReadOnlyList<PortalEntry>> Split(IReadOnlyList<PortalEntry> portals, int budget)
        {
            if (portals == null) throw new ArgumentNullException(nameof(portals));
            var pages = new List<IReadOnlyList<PortalEntry>>();
            var page = new List<PortalEntry>();
            int size = PortalListCodec.PageHeaderSize;
            foreach (var portal in portals)
            {
                int entry = PortalListCodec.EntrySize(portal);
                if (PortalListCodec.PageHeaderSize + entry > budget) throw new ArgumentException("One portal exceeds the page budget.", nameof(budget));
                if (page.Count > 0 && (size + entry > budget || page.Count >= PortalListCodec.MaxEntriesPerPage))
                {
                    pages.Add(page);
                    page = new List<PortalEntry>();
                    size = PortalListCodec.PageHeaderSize;
                }
                page.Add(portal);
                size += entry;
            }
            pages.Add(page);
            if (pages.Count > PortalListCodec.MaxPages) throw new InvalidOperationException("The portal list exceeds " + PortalListCodec.MaxPages + " pages.");
            return pages;
        }
    }

    /// <summary>The client side: which page to ask for next, and the last complete list. A page of another version than
    /// the one being collected starts the collection again at page 0; a page out of turn is ignored; the unchanged
    /// answer keeps the list held.</summary>
    public sealed class PortalListAssembly
    {
        private readonly List<PortalEntry> collecting = new List<PortalEntry>();
        private long collectingVersion;
        private int nextPage;
        private IReadOnlyList<PortalEntry> portals = Array.Empty<PortalEntry>();

        /// <summary>Version of the complete list held; 0 for none.</summary>
        public long Held { get; private set; }

        /// <summary>The last complete list (empty before the first).</summary>
        public IReadOnlyList<PortalEntry> Portals => portals;

        /// <summary>Changes whenever a new complete list replaces the held one.</summary>
        public int Generation { get; private set; }

        public PortalListRequest Next() => new PortalListRequest(Held, nextPage);

        /// <summary>Takes one answer; true when it completed a new list.</summary>
        public bool Accept(PortalListPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (page.IsUnchanged)
            {
                if (page.Version != Held) Restart();
                return false;
            }
            if (page.Page == 0)
            {
                collecting.Clear();
                collectingVersion = page.Version;
                nextPage = 0;
            }
            else if (page.Version != collectingVersion || page.Page != nextPage)
            {
                if (page.Version != collectingVersion) Restart();
                return false;
            }
            collecting.AddRange(page.Entries);
            nextPage = page.Page + 1;
            if (nextPage < page.Pages) return false;
            portals = collecting.ToArray();
            Held = page.Version;
            Generation++;
            Restart();
            return true;
        }

        public void Reset()
        {
            Restart();
            portals = Array.Empty<PortalEntry>();
            Held = 0L;
            Generation++;
        }

        private void Restart()
        {
            collecting.Clear();
            collectingVersion = 0L;
            nextPage = 0;
        }
    }
}
