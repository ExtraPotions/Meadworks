// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The boss owner's half of a Brutal throw: which hits carry it. Whether a blow is heavy is known only on the boss's
    /// owner, where the game runs the attack, but the player it hits is judged on their own client. So while a Brutal
    /// boss's heavy blow is being dealt (<see cref="Begin"/> to <see cref="End"/>, around the game's melee sweep or area
    /// burst), each hit it sends to a player is marked: HitData.m_skillRaiseAmount = <see cref="Mark"/>. The game sends
    /// that field with the hit whenever it is not 1, and nothing on a player's side of a hit reads it - the game raises an
    /// attacker's skill from the attack, never from the hit - so the mark reaches the struck player's client intact and
    /// changes nothing else. The hits are stamped at the last moment, as each leaves for the struck player's owner, so the
    /// mark rides on exactly the hits of that blow: other targets, other blows and other bosses never carry it.
    /// </summary>
    internal static class BrutalBlow
    {
        /// <summary>HitData.m_skillRaiseAmount of a hit that throws the player it lands on: negative, which no attack of
        /// the game's ever sends (its own default is 1).</summary>
        public const float Mark = -18f;

        private static ZDOID _boss = ZDOID.None;

        /// <summary>True only while a Brutal boss's heavy blow is being dealt on its owner; the hit patch reads only this
        /// until then.</summary>
        public static bool Live { get; private set; }

        /// <summary>A boss's blow is about to be dealt on this machine: open the marking if it is a Brutal heavy one, and
        /// say whether this call opened it (only that call's end closes it).</summary>
        public static bool Begin(Attack attack, Humanoid boss)
        {
            BrutalBehaviour brutal = boss.GetComponent<BrutalBehaviour>();
            if (Live || brutal == null || !brutal.Throws(attack))
            {
                return false;
            }
            _boss = boss.GetZDOID();
            Live = true;
            return true;
        }

        /// <summary>The blow is dealt, or cut short: whatever is sent from now on is unmarked.</summary>
        public static void End()
        {
            Live = false;
            _boss = ZDOID.None;
        }

        /// <summary>A hit of the open blow leaving for its target: mark it if the target is a player.</summary>
        public static void Stamp(Character target, HitData hit)
        {
            if (target is Player && hit.m_attacker == _boss)
            {
                hit.m_skillRaiseAmount = Mark;
            }
        }

        /// <summary>True when a hit was marked as a Brutal heavy blow's; any machine may ask.</summary>
        public static bool Carries(HitData hit) => hit.m_skillRaiseAmount == Mark;
    }
}
