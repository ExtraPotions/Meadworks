// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Commands;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Registers the mod's console commands once the terminal system initialises. <c>Terminal.InitTerminal</c> runs as
    /// the game builds its command table (and again for the chat terminal), so this is the moment custom commands must
    /// be added; <see cref="DamageCommand.Register"/> is idempotent, so a second call is a no-op. The table is shared:
    /// the chat runs a line starting with "/" as the command it names, so it works in the F5 console and in chat. The
    /// adopted <c>deaths</c> command is not registered: the death recap opens from the MeadWorks menu or its own key.
    /// </summary>
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    public static class TerminalCommandPatch
    {
        private static void Postfix() => Guard.Run("Terminal.InitTerminal register", Register);

        private static void Register()
        {
            DamageCommand.Register();
        }
    }
}
