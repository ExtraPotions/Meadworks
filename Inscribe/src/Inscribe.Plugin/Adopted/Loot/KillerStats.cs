// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Effects;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Reads the killer's loot-find totals at a death (drops.md section 10). The killer is the creature's last hitter
    /// (DECISIONS DRP-12): the game records it in <c>Character.m_lastHit</c> on the creature's owner for every hit that
    /// lands while the creature is alive, the killing blow included (verified in <c>Character.ApplyDamage</c>). A fall,
    /// a drowning or a pet's last hit leaves no player there, so no bonus. The totals come from the killer's own player
    /// ZDO, which the killer's client publishes (<see cref="FindPublisher"/>); only player ZDOs ever carry the keys, so
    /// any other attacker reads as 0. Each value is clamped to the running rules' cap for its channel, and a stat no
    /// affix in the rules feeds reads as 0. Runs on the dying creature's owner; reads replicated data only.
    /// </summary>
    internal static class KillerStats
    {
        public static LootModifiers Read(Character creature)
        {
            ZDO? zdo = KillerZdo(creature);
            if (zdo == null || !ItemEffects.Enabled)
            {
                return LootModifiers.None;
            }
            FindChannels channels = FindChannels.Current;
            return new LootModifiers(
                Stat(zdo, channels, FindStat.Rarity),
                Stat(zdo, channels, FindStat.Stones),
                Stat(zdo, channels, FindStat.Trophy),
                Stat(zdo, channels, FindStat.Coins));
        }

        private static ZDO? KillerZdo(Character creature)
        {
            HitData? hit = creature != null ? creature.m_lastHit : null;
            if (hit == null || hit.m_attacker.IsNone() || ZDOMan.instance == null)
            {
                return null;
            }
            return ZDOMan.instance.GetZDO(hit.m_attacker);
        }

        private static float Stat(ZDO zdo, FindChannels channels, FindStat stat) =>
            channels.Clamp((int)stat, zdo.GetFloat(FindKeys.Hashes[(int)stat], 0f));
    }
}
