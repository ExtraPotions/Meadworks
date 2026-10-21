// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a file's text kept while its size and write time are unchanged.
using System;

namespace ExtraPotions.Wildblood.Integration
{
    /// <summary>
    /// A polled file's text, read again only when its length or last write time changes, so the two-second rules poll does not
    /// reread an unchanged file. Main thread only.
    /// </summary>
    public sealed class FileTextCache
    {
        string? text;
        long length = -1;
        DateTime written;

        public string Get(long fileLength, DateTime lastWriteUtc, Func<string> read)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (text == null || fileLength != length || lastWriteUtc != written)
            {
                // Cleared first so a failed read is not served from the old entry.
                text = null;
                var fresh = read();
                text = fresh; length = fileLength; written = lastWriteUtc;
            }
            return text!;
        }

        public void Clear() { text = null; length = -1; written = default; }
    }
}
