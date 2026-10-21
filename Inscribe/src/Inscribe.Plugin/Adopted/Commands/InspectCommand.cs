// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe inspect [cursor|hover|ground|&lt;slot&gt;]</c>: dumps an item's <c>ecf_</c> data and its parse
    /// (console-commands.md section 3). Read-only; any item the local player can see, including one on the ground
    /// (the client already has its replicated data, no ownership needed).
    /// </summary>
    internal static class InspectCommand
    {
        public const string Grammar = "inscribe inspect [" + ItemTargets.AnyWords + "]";

        public static void Run(CommandCall call)
        {
            string word = call.Lower(0);
            if (word.Length > 0 && !ItemTargets.IsWord(word, allowGround: true))
            {
                call.Fail($"unknown target '{word}'.", Grammar);
                return;
            }
            ItemDrop.ItemData? item = ItemTargets.Resolve(word, allowGround: true, out string where);
            if (item == null)
            {
                string problem = word.Length == 0
                    ? "no item on the cursor, under the mouse, under the crosshair or in the right hand."
                    : $"no item at '{word}'.";
                call.Fail(problem, Grammar);
                return;
            }
            ItemReport.Write(call, item, where);
        }
    }
}
