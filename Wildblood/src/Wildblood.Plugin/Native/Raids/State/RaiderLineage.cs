// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// What a dying raider hands down to its Splintering copies (features/raids.md section 3: "Splintering copies carry
    /// none"): its host, its raid and its role, with no coins and no Warlord's mark, and the loot multiplier it would have
    /// paid at. A tagged copy is a raider like the rest - it goes for the base, dies dropping nothing when the raid is
    /// stopped, walks off when it is lost, and drops its own loot times the raid's drops - but it is on no wave's roster
    /// (<see cref="RaidRoster"/>, which only the host's owner writes), so the raid is won once the waves are beaten and a
    /// copy left then walks off. Read from the parent's ZDO in the death patch's prefix while the ZDO still answers
    /// (<see cref="Patches.DeathPatch"/>), and written by <see cref="Mutations.Splitter"/> into each copy in the frame it
    /// is made, on the machine that makes it and so owns it. A creature that is no raider hands down nothing.
    /// </summary>
    public sealed class RaiderLineage
    {
        private readonly ZDOID _host;
        private readonly long _raid;
        private readonly RaiderRole _role;
        private readonly float _drops;

        private RaiderLineage(ZDOID host, long raid, RaiderRole role, float drops)
        {
            _host = host;
            _raid = raid;
            _role = role;
            _drops = drops;
        }

        /// <summary>The tag a raider's copies take; null for a creature that is no raider. Its owner, as it dies.</summary>
        public static RaiderLineage? Read(ZDO parent) => RaiderTag.IsRaider(parent)
            ? new RaiderLineage(RaiderTag.Host(parent), RaiderTag.Raid(parent), RaiderTag.Role(parent),
                RaiderPurse.DropsFor(parent))
            : null;

        /// <summary>Tags one copy with no coins, on the machine that just made it, before its AI starts.</summary>
        public void Apply(ZDO copy) => RaiderTag.Write(copy, _host, _raid, _role, false, 0, _drops);
    }
}
