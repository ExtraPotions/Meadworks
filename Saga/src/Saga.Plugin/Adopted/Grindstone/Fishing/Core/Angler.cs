// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Fishing/Core/Angler.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The angler behind a fishing float. The float is made on the caster's client when the cast lands (the thrown
    /// projectile's spawn on hit calls FishingFloat.Setup there), so the caster owns it and runs its FixedUpdate: the
    /// reel, the hook and the catch all happen on the angler's own client. Setup's postfix (<see cref="FloatSetup"/>)
    /// writes the angler's Fishing level to the float's ZDO, which reaches every machine near it, so the owners of the
    /// fish around it can read it when they decide whether to bite (<see cref="BiteChance"/>). A float cast without
    /// GrindstoneSkills reads as level 0.
    /// </summary>
    public static class Angler
    {
        private static readonly int AnglerLevelHash = Keys.AnglerLevel.GetStableHashCode();

        public static void Stamp(FishingFloat fishingFloat, float level) =>
            fishingFloat.m_nview.GetZDO().Set(Keys.AnglerLevel, level);

        /// <summary>The Fishing level of the float's angler at the cast; 0 when unknown.</summary>
        public static float Level(FishingFloat fishingFloat)
        {
            ZDO zdo = Zdo(fishingFloat);
            return zdo == null ? 0f : UnityEngine.Mathf.Max(0f, zdo.GetFloat(AnglerLevelHash));
        }

        /// <summary>
        /// The local player, when this client owns the float and the local player cast it; null anywhere else. The game
        /// finds a float's angler by the user ID stored at the cast (FishingFloat.GetOwner).
        /// </summary>
        public static Player LocalOf(FishingFloat fishingFloat)
        {
            if (fishingFloat == null || fishingFloat.m_nview == null || !fishingFloat.m_nview.IsValid() || !fishingFloat.m_nview.IsOwner())
                return null;
            Player local = Player.m_localPlayer;
            return local != null && fishingFloat.GetOwner() == local ? local : null;
        }

        private static ZDO Zdo(FishingFloat fishingFloat) =>
            fishingFloat != null && fishingFloat.m_nview != null && fishingFloat.m_nview.IsValid() ? fishingFloat.m_nview.GetZDO() : null;
    }
}
