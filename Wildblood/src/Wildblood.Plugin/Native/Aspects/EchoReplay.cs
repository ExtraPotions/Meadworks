// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The echo does again what its boss did, on the echo's owner. An animation cue is sent to the echo's animator (on
    /// every machine, as the game sends any cue). An attack is started through the game's own attack start, with the weapon
    /// the boss swung and the random state it started with, so the echo plays the same swing, and its blow, throw, breath
    /// or slam lands for real: the game's attack code hits whoever stands where the boss struck `delay` seconds ago, with
    /// the boss's own strength (<see cref="EchoLink.Source"/>). When the echo cannot start it (still finishing the swing
    /// before, say) it plays the motion alone, harmless.
    /// </summary>
    internal static class EchoReplay
    {
        public static void Play(Character echo, EchoEvent echoEvent)
        {
            ZSyncAnimation anim = echo.m_zanim;
            if (anim == null)
            {
                return;
            }
            if (echoEvent.Trigger != null)
            {
                anim.SetTrigger(echoEvent.Trigger);
                return;
            }
            EchoAttack? attack = echoEvent.Attack;
            if (attack == null || (echo is Humanoid humanoid && Strike(humanoid, attack)))
            {
                return;
            }
            foreach (string cue in attack.Cues)
            {
                anim.SetTrigger(cue);
            }
        }

        private static bool Strike(Humanoid echo, EchoAttack attack)
        {
            if (!Arm(echo, attack.Weapon))
            {
                return false;
            }
            Random.State before = Random.state;
            Random.state = attack.Seed;
            try
            {
                return echo.StartAttack(null, attack.Secondary);
            }
            finally
            {
                Random.state = before;
            }
        }

        /// <summary>The echo holds the weapon the boss swung: already, or equipped now from its own gear.</summary>
        private static bool Arm(Humanoid echo, string weapon)
        {
            if (Holds(echo, weapon))
            {
                return true;
            }
            foreach (ItemDrop.ItemData item in echo.GetInventory().GetAllItems())
            {
                if (item.IsWeapon() && item.m_shared.m_name == weapon)
                {
                    echo.EquipItem(item, triggerEquipEffects: false);
                    break;
                }
            }
            return Holds(echo, weapon);
        }

        private static bool Holds(Humanoid echo, string weapon)
        {
            ItemDrop.ItemData current = echo.GetCurrentWeapon();
            return current != null && current.m_shared.m_name == weapon;
        }
    }
}
