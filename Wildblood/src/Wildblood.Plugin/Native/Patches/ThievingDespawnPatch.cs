// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; receipted drops, pouch kept when undecidable.
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The one path that removes a marked creature without ever running <c>Character.OnDeath</c>: vanilla's own
    /// "despawn in day" AI exit (<c>MonsterAI.DespawnInDay</c>) walks the creature away and then calls
    /// <c>BaseAI.MoveAwayAndDespawn</c>, which network-destroys it directly. Named here rather than left as a silent
    /// hole, per the spec's "goods always drop" table - "Despawned by the game: Goods drop where it stood." A prefix,
    /// so the pouch is read while the ZDO is still valid, exactly as <c>Commands.PurgeCommand</c> already does before
    /// its own destroy call. The game calls this every AI tick while the creature walks away and destroys it only on
    /// the tick no player is within <see cref="DespawnRange"/>, so the pouch drops on that tick alone - dropping it on
    /// every walk-away tick duplicated the stolen goods.
    /// </summary>
    [HarmonyPatch(typeof(BaseAI), "MoveAwayAndDespawn")]
    public static class ThievingDespawnPatch
    {
        /// <summary>BaseAI.MoveAwayAndDespawn's own range: with a player this close it walks away instead of vanishing.</summary>
        private const float DespawnRange = 40f;

        // Every creature walking off to despawn comes through here on every AI tick, on the server too: the first test
        // is two field reads and one ZDO lookup, and only a creature carrying stolen goods goes any further.
        // Returning false keeps the creature (and its pouch) for this tick: only when a pouch entry could be neither
        // dropped nor durably decided, so an undecidable pouch is never destroyed with its carrier.
        private static bool Prefix(BaseAI __instance)
        {
            ZNetView nview = __instance.m_nview;
            if (nview != null && nview.IsValid() && nview.IsOwner() && PouchStore.Count(nview.GetZDO()) > 0)
            {
                bool keep = true; // a throw keeps the creature: its pouch is never destroyed undecided
                Guard.Run("BaseAI.MoveAwayAndDespawn thieving", ai => keep = !DropPouch(ai), __instance);
                return !keep;
            }
            return true;
        }

        private static bool DropPouch(BaseAI ai)
        {
            Character character = ai.m_character;
            if (character == null || Player.GetClosestPlayer(character.transform.position, DespawnRange) != null)
            {
                return true; // only walking away this tick: the destroy has not come yet
            }
            ZDO zdo = ai.m_nview.GetZDO();
            return PouchDrop.DropAll(PouchStore.Load(zdo), character.transform.position, zdo.m_uid, zdo, lastChance: false);
        }
    }
}
