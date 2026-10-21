// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the one place a `wildblood` console verb writes its answer. Replaces the adopted `elite` root.
namespace ExtraPotions.Wildblood.Adopted.Commands
{
    public static class CommandReply
    {
        public static void Send(Terminal.ConsoleEventArgs args, string text) => args?.Context?.AddString(text);
    }
}
