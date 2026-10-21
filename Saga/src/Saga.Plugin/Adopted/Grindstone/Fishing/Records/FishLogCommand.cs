// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Records/FishLogCommand.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The <c>fishlog</c> command, in the console (F5) or in chat as <c>/fishlog</c>: opens the local character's
    /// Angler's Log window, the same window as the Fishing row's button in the Skills window. Not a cheat, so chat allows
    /// it. Registered once, in a Terminal.InitTerminal postfix, as other mods in this workspace register theirs.
    /// </summary>
    public static class FishLogCommand
    {
        private static Terminal.ConsoleCommand command;

        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        private static class Init
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            private static void Postfix()
            {
                Ensure();
            }
        }

        public static void Ensure() {
            if (HarmonyLib.AccessTools.Field(typeof(Terminal), "commands")?.GetValue(null) is System.Collections.IDictionary) {
                if (command != null || !NativeSaga.Active("fishing"))
                    return;
                command = new Terminal.ConsoleCommand("fishlog", SagaText.Text("gs_b79d9f795cd0858e"),
                    args => HookGuard.Run("fishlog", () => Print(args)));
            }
        }

        public static void Shutdown() {
            if (HarmonyLib.AccessTools.Field(typeof(Terminal), "commands")?.GetValue(null) is System.Collections.IDictionary commands && commands.Contains("fishlog") && object.ReferenceEquals(commands["fishlog"], command)) commands.Remove("fishlog");
            command = null;
        }

        private static void Print(Terminal.ConsoleEventArgs args)
        {
            if (!NativeSaga.Active("fishing")) return;
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                args.Context.AddString(SagaText.Text("gs_7db63e3e84d75329"));
                return;
            }
            NativeAngler.Open();
        }
    }
}
