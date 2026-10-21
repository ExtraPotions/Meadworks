// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// One invocation of <c>inscribe</c>: the sub-command, its arguments (empty words from double spaces dropped) and the
    /// console that ran it. Output is English only (DECISIONS.md LOC-3) and goes to that console: the first line of a
    /// reply starts with <c>inscribe &lt;sub&gt;:</c>, detail lines are indented.
    /// </summary>
    internal sealed class CommandCall
    {
        private readonly Terminal? _console;
        private readonly List<string> _args = new List<string>();

        public CommandCall(Terminal.ConsoleEventArgs args)
        {
            _console = args.Context;
            for (int i = 1; i < args.Length; i++)
            {
                if (!string.IsNullOrEmpty(args[i]))
                {
                    _args.Add(args[i]);
                }
            }
            Sub = _args.Count > 0 ? _args[0].ToLowerInvariant() : "help";
        }

        /// <summary>The sub-command, lowercase; <c>help</c> when none was given.</summary>
        public string Sub { get; }

        /// <summary>Arguments after the sub-command.</summary>
        public int Count => _args.Count > 0 ? _args.Count - 1 : 0;

        /// <summary>Argument <paramref name="index"/> after the sub-command as typed, or "" when absent.</summary>
        public string Arg(int index) => index + 1 < _args.Count ? _args[index + 1] : "";

        /// <summary>The same, lowercase (sub-commands and ids are case-insensitive).</summary>
        public string Lower(int index) => Arg(index).ToLowerInvariant();

        /// <summary>A line starting with <c>inscribe &lt;sub&gt;:</c>.</summary>
        public void Reply(string text) => ReplyPlain($"{EcraftCommand.Name} {Sub}: {text}");

        /// <summary>An indented detail line under the reply.</summary>
        public void Detail(string text) => ReplyPlain("  " + text);

        /// <summary>The problem, then the grammar line.</summary>
        public void Fail(string problem, string grammar)
        {
            Reply(problem);
            Detail("usage: " + grammar);
        }

        /// <summary>A raw console line. On a headless server nobody sees the console, so the line is logged too.</summary>
        public void ReplyPlain(string line)
        {
            _console?.AddString(line);
            if (Headless)
            {
                Core.Log.Info(line);
            }
        }

        private static bool Headless =>
            UnityEngine.SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }
}
