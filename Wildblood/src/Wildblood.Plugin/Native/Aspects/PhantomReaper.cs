// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// When a Phantom boss dies, its remaining copies go with it. Runs on the dying boss's owner from the death patch;
    /// the copies stand around the boss, so every one still standing is loaded here. Each is sent the vanish through its
    /// own network view, to whichever machine owns it, which lets it fall - hollow, so no drops and no body.
    /// </summary>
    internal static class PhantomReaper
    {
        public static void Release(ZDOID boss)
        {
            if (boss == ZDOID.None)
            {
                return;
            }
            int released = 0;
            foreach (Character character in Character.GetAllCharacters())
            {
                if (IsCopyOf(character, boss))
                {
                    CreatureRpc.Vanish(character);
                    released++;
                }
            }
            if (Log.Diagnostics)
            {
                Log.Diag($"phantom boss {boss} fell; {released} copies released");
            }
        }

        private static bool IsCopyOf(Character character, ZDOID boss)
        {
            ZNetView nview = character.GetComponent<ZNetView>();
            ZDO? zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            return zdo != null && !character.IsDead() && AspectStore.GetPhantomOf(zdo) == boss;
        }
    }
}
