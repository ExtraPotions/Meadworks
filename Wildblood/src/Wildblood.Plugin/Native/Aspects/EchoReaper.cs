// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// When an Echoing boss dies, its echo is simply gone - no fall, no puff, no body. Runs on the dying boss's owner from
    /// the death patch; the echo walks the boss's own path, so it is loaded here, and normally owned here too. One owned
    /// elsewhere is taken over first, so its removal reaches every machine. The echo's own check catches the rest: an
    /// echo whose boss is dead or gone takes itself away (<see cref="EchoBody"/>).
    /// </summary>
    internal static class EchoReaper
    {
        public static void Release(ZDOID boss)
        {
            Character? echo = boss != ZDOID.None ? EchoLink.Find(boss) : null;
            ZNetView? view = echo != null ? echo.GetComponent<ZNetView>() : null;
            if (echo == null || view == null || !view.IsValid() || ZNetScene.instance == null)
            {
                return;
            }
            if (!view.IsOwner())
            {
                view.ClaimOwnership();
            }
            ZNetScene.instance.Destroy(echo.gameObject);
            if (Log.Diagnostics)
            {
                Log.Diag($"echoing boss {boss} fell; its echo went with it");
            }
        }
    }
}
