// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Barter.Runtime
{
    internal sealed class ListEditRequest
    {
        public string Name = "", Revision = "", Text = "";
    }

    internal sealed class ListEditCodec : IOperationCodec<ListEditRequest, bool>
    {
        internal const int TextLimit = 45000;
        internal const int RequestLimit = 16384;

        internal static bool FitsRequest(string name, string revision, string text)
        {
            try { new ListEditCodec().EncodeRequest(new ListEditRequest { Name = name, Revision = revision, Text = text }); return true; }
            catch (InvalidDataException) { return false; }
        }

        public byte[] EncodeRequest(ListEditRequest request)
        {
            if (!ValidFields(request)) throw new InvalidDataException("Invalid list edit fields");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(request.Name); writer.Write(request.Revision); writer.Write(request.Text);
                if (stream.Length > RequestLimit) throw new InvalidDataException("List edit payload exceeds operation bound");
                return stream.ToArray();
            }
        }
        public ListEditRequest DecodeRequest(byte[] bytes)
        {
            if (bytes == null || bytes.Length > RequestLimit) throw new InvalidDataException();
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
            {
                var request = new ListEditRequest { Name = reader.ReadString(), Revision = reader.ReadString(), Text = reader.ReadString() };
                if (stream.Position != stream.Length || !ValidFields(request))
                    throw new InvalidDataException();
                return request;
            }
        }
        private static bool ValidFields(ListEditRequest request) => request != null && request.Name != null &&
            request.Name.Length <= 256 && request.Revision != null && request.Revision.Length == 44 &&
            request.Text != null && Encoding.UTF8.GetByteCount(request.Text) <= TextLimit;
        public byte[] EncodeReply(bool reply) => new[] { reply ? (byte)1 : (byte)0 };
        public bool DecodeReply(byte[] bytes)
        { if (bytes == null || bytes.Length != 1 || bytes[0] > 1) throw new InvalidDataException(); return bytes[0] == 1; }
    }
}
