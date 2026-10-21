// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;

namespace ExtraPotions.Thing.Moderation
{
    public static class GameListIdentity
    {
        public static bool Valid(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 160) return false;
            if (text.All(c => c >= '0' && c <= '9')) return true;
            var separator = text.IndexOf('_');
            return separator > 0 && separator < text.Length - 1 && text.Substring(0, separator).All(c => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z')
                && text.Substring(separator + 1).All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ':' || c == '.');
        }
    }
}
